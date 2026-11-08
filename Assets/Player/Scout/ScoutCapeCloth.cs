using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

/// <summary>Render-only, pinned particle FABRIC, not animated cape bones. Fixed
/// substeps solve stretch/shear/bend and anatomical capsule contacts. Uses the
/// approved atlas, never writes the motor/rig or adds native bodies/colliders.
/// Managed mesh/constraints also avoid relying on platform-native cloth support.</summary>
[DefaultExecutionOrder(105)]
[DisallowMultipleComponent]
public sealed class ScoutCapeCloth : MonoBehaviour
{
    public const int Columns=13,Rows=21,ParticleCount=Columns*Rows;
    public const string MeshName="Scout longer cape cloth (runtime)";
    [Tooltip("Shoulder-to-hem metres at source Scout scale1 (scene visual is normally2). Original cape was .62m.")]
    [Range(.7f,1.3f)] public float capeLength=.98f;
    [Tooltip("Local cosmetic airflow in world m/s. Does not change global wind, physics or player motion.")]
    public Vector3 ambientWind=new Vector3(2,.2f,-2);
    [Range(0,8)] public float gustStrength=1.8f;
    [Range(0,5)] public float airDrag=1.8f;
    [Range(4,16)] public int solverIterations=8;
    public bool bodyCollision=true;
    [Tooltip("Leave on for gameplay. Off permits deterministic owned preview/test ticks only.")]
    public bool automaticSimulation=true;
    public bool Initialized { get; private set; }
    public Mesh FabricMesh { get { return mesh; } }
    public Renderer FabricRenderer { get { return renderer; } }
    public float SourceLength { get { return safeLength; } }
    public int ResetCount { get; private set; }
    public string Error { get; private set; }

    struct Link { public int a,b;public float rest,stiffness;public bool maximumOnly; }
    struct Capsule { public Vector3 a,b,axis,min,max;public float radius,width,inverseLength,paddedRadius,paddedWidth,broadRadiusSquared;public Quaternion rotation,inverseRotation; }
    static readonly ProfilerMarker SimulationMarker=new ProfilerMarker("ODM.ScoutCape.Simulate");
    readonly Vector3[] positions=new Vector3[ParticleCount],previous=new Vector3[ParticleCount],rest=new Vector3[ParticleCount];
    readonly float[] maximumReach=new float[ParticleCount];
    readonly Vector3[] normals=new Vector3[ParticleCount];
    readonly Capsule[] capsules=new Capsule[7];
    Vector3[] vertices,renderNormals;
    Vector4[] tangents;
    Vector2[] uv,emblem;
    Link[] links,structuralLinks;
    Transform model,chest,neck,hips,spine,leftShoulder,rightShoulder,leftThigh,rightThigh,leftKnee,rightKnee;
    SkinnedMeshRenderer original;
    new MeshRenderer renderer;
    Mesh mesh;
    GameObject fabric;
    PlayerHealth health;
    bool originalEnabled,reverseForward;
    float scale,safeLength,clock,accumulator;
    Vector3 oldOrigin,referenceVelocity;
    Quaternion oldFrame;

    void LateUpdate()
    {
        if(health!=null && health.isDead)return; // Death snapshots own their mesh.
        if(!Initialized)
        {
            var driver=GetComponent<ScoutPlayerAnimation>();
            if(driver==null || driver.model==null || !Initialize(driver.model))return;
        }
        if(automaticSimulation)using(SimulationMarker.Auto())Simulate(Time.deltaTime);
    }
    public bool Initialize(Transform visual)
    {
        if(Initialized)return true;
        if(visual==null || !visual.IsChildOf(transform)){Error="Cape needs this player's own visual";return false;}
        var named=new Dictionary<string,Transform>();foreach(var t in visual.GetComponentsInChildren<Transform>(true))if(!named.ContainsKey(t.name))named.Add(t.name,t);
        string[] required={"Chest","Neck","Hips","Spine","LeftShoulder","RightShoulder","LeftUpperLeg","RightUpperLeg","LeftLowerLeg","RightLowerLeg"};
        foreach(var n in required)if(!named.ContainsKey(n)){Error="Missing cape anchor "+n;return false;}
        foreach(var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.name=="ScoutCape"){original=r;break;}
        if(original==null || original.sharedMaterial==null){Error="Approved ScoutCape renderer/material missing";return false;}
        model=visual;chest=named["Chest"];neck=named["Neck"];hips=named["Hips"];spine=named["Spine"];
        leftShoulder=named["LeftShoulder"];rightShoulder=named["RightShoulder"];leftThigh=named["LeftUpperLeg"];rightThigh=named["RightUpperLeg"];
        leftKnee=named["LeftLowerLeg"];rightKnee=named["RightLowerLeg"];health=GetComponent<PlayerHealth>();
        reverseForward=Vector3.Dot(Vector3.Cross(rightShoulder.position-leftShoulder.position,neck.position-chest.position),model.forward)<0;
        scale=Mathf.Max(.01f,Mathf.Abs(model.lossyScale.y));safeLength=Safe(capeLength,.98f,.7f,1.3f);
        BuildRestAndLinks();BuildMesh();
        fabric=new GameObject("Scout simulated fabric");fabric.hideFlags=HideFlags.DontSave;fabric.layer=original.gameObject.layer;fabric.transform.SetParent(chest,false);
        fabric.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=fabric.AddComponent<MeshRenderer>();renderer.sharedMaterials=original.sharedMaterials;
        renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
        originalEnabled=original.enabled;original.enabled=false;Initialized=true;Error=null;ResetSimulation();return true;
    }
    static float Safe(float value,float fallback,float min,float max)
    { return float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp(value,min,max); }
    static bool Finite(Vector3 p)
    { return !float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z); }
    Quaternion Frame()
    {
        Vector3 up=(neck.position-chest.position).normalized;
        Vector3 forward=Vector3.Cross(rightShoulder.position-leftShoulder.position,up).normalized*(reverseForward?-1:1);
        return forward.sqrMagnitude<.5f?model.rotation:Quaternion.LookRotation(forward,up);
    }
    static float Width(float f)
    { return f<.15f?Mathf.Lerp(.12f,.25f,f/.15f):Mathf.Lerp(.25f,.235f,(f-.15f)/.85f); }
    void BuildRestAndLinks()
    {
        for(int row=0;row<Rows;row++)for(int col=0;col<Columns;col++)
        {
            float f=row/(float)(Rows-1),u=col/(float)(Columns-1);
            rest[row*Columns+col]=new Vector3((u*2-1)*Width(f),-safeLength*f,-.12f*f);
        }
        var all=new List<Link>();
        for(int r=0;r<Rows;r++)for(int c=0;c<Columns;c++)
        {
            int a=r*Columns+c;
            if(c+1<Columns)AddLink(all,a,a+1,1);
            if(r+1<Rows)AddLink(all,a,a+Columns,1);
            if(r+1<Rows && c+1<Columns){AddLink(all,a,a+Columns+1,.85f);AddLink(all,a+1,a+Columns,.85f);}
            if(c+2<Columns)AddLink(all,a,a+2,.15f);
            if(r+2<Rows)AddLink(all,a,a+Columns*2,.15f);
        }
        // Multiresolution strain limits propagate collar tension efficiently.
        // They only resist excess length, not compression/folding, so the cloth
        // can still wrinkle instead of becoming a stiff two-bone flap.
        foreach(int span in new[]{4,8,16})for(int r=0;r+span<Rows;r++)for(int c=0;c<Columns;c++)
        {
            int a=r*Columns+c,b=(r+span)*Columns+c;float length=0;
            for(int j=a;j<b;j+=Columns)length+=Vector3.Distance(rest[j],rest[j+Columns]);
            all.Add(new Link{a=a,b=b,rest=length*1.01f,stiffness=1,maximumOnly=true});
        }
        links=all.ToArray();
        // Same projection order/limits, cached only when length or scale changes.
        var structural=new List<Link>();foreach(var link in links)if(link.stiffness==1 && !link.maximumOnly)structural.Add(link);
        structuralLinks=structural.ToArray();
        for(int i=Columns;i<ParticleCount;i++)maximumReach[i]=Vector3.Distance(rest[i],rest[i%Columns])*scale*1.025f;
    }
    void AddLink(List<Link> all,int a,int b,float stiffness)
    { all.Add(new Link{a=a,b=b,rest=Vector3.Distance(rest[a],rest[b]),stiffness=stiffness}); }
    void BuildMesh()
    {
        // Original ten faceted wing shapes, now attached to interpolated fabric
        // positions instead of disconnected rigid cape-bone geometry.
        var logo=new List<Vector2>();
        foreach(int side in new[]{-1,1})for(int j=0;j<5;j++)
        {
            float x=side*(.012f+j*.023f),y=1.32f-j*.017f;
            logo.Add(new Vector2(x,y));logo.Add(new Vector2(x+side*.028f,y+.015f));
            logo.Add(new Vector2(x+side*.043f,y+.088f));logo.Add(new Vector2(x+side*.019f,y+.042f));
        }
        emblem=logo.ToArray();int count=ParticleCount*2+emblem.Length;
        vertices=new Vector3[count];renderNormals=new Vector3[count];tangents=new Vector4[count];uv=new Vector2[count];
        var triangles=new List<int>();
        for(int r=0;r<Rows;r++)for(int c=0;c<Columns;c++)
        {
            int i=r*Columns+c;float u=c/(float)(Columns-1),f=r/(float)(Rows-1);
            uv[i]=uv[ParticleCount+i]=new Vector2((.07f+.86f*u)/4,(1+.07f+.86f*(1-f))/4);
            if(r+1<Rows && c+1<Columns)
            {
                int a=i,b=i+1,d=i+Columns+1,e=i+Columns;
                triangles.AddRange(new[]{a,b,e,b,d,e,ParticleCount+a,ParticleCount+e,ParticleCount+b,ParticleCount+b,ParticleCount+e,ParticleCount+d});
            }
        }
        for(int i=0;i<emblem.Length;i++)uv[ParticleCount*2+i]=new Vector2((.2f+(i%2)*.6f)/4,(3+.2f+(i%3)*.25f)/4);
        for(int i=0;i<emblem.Length;i+=4)
        {
            int a=ParticleCount*2+i;
            triangles.AddRange(new[]{a,a+1,a+2,a,a+2,a+3,a+2,a+1,a,a+3,a+2,a});
        }
        mesh=new Mesh{name=MeshName};mesh.MarkDynamic();mesh.vertices=vertices;mesh.uv=uv;mesh.SetTriangles(triangles,0);
    }
    public void ResetSimulation()
    {
        if(!Initialized)return;
        var frame=Frame();Vector3 origin=chest.position+frame*new Vector3(0,.24f,-.115f)*scale;
        for(int i=0;i<ParticleCount;i++)positions[i]=previous[i]=origin+frame*rest[i]*scale;
        oldOrigin=origin;oldFrame=frame;referenceVelocity=Vector3.zero;accumulator=0;ResetCount++;
        UpdateCapsules(frame);for(int i=Columns;i<ParticleCount;i++)ProjectBody(i);
        WriteMesh();
    }
    public void Simulate(float dt)
    {
        if(!Initialized || !isActiveAndEnabled)return;
        if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.2f){ResetSimulation();return;}
        if(dt==0)return;
        float nextScale=Mathf.Max(.01f,Mathf.Abs(model.lossyScale.y)),nextLength=Safe(capeLength,.98f,.7f,1.3f);
        if(Mathf.Abs(nextScale-scale)>.001f || Mathf.Abs(nextLength-safeLength)>.001f)
        {scale=nextScale;safeLength=nextLength;BuildRestAndLinks();ResetSimulation();return;}
        Quaternion frame=Frame();Vector3 origin=chest.position+frame*new Vector3(0,.24f,-.115f)*scale,delta=origin-oldOrigin;
        if(!Finite(origin) || delta.magnitude>Mathf.Max(3*scale,120*dt) || Quaternion.Angle(oldFrame,frame)>120)
        {ResetSimulation();return;}
        // Solve in a transported frame: numerical stability at ODM80m/s without
        // making the fabric rigid. Relative airflow/inertial forces deform every
        // free vertex; only the shoulder row is pinned. No gameplay transform writes.
        Quaternion rotation=frame*Quaternion.Inverse(oldFrame);Vector3 velocity=delta/dt;
        Vector3 inertia=Vector3.ClampMagnitude(-(velocity-referenceVelocity)/dt,70);
        for(int i=0;i<ParticleCount;i++)
        {positions[i]=origin+rotation*(positions[i]-oldOrigin);previous[i]=origin+rotation*(previous[i]-oldOrigin);}
        referenceVelocity=velocity;oldOrigin=origin;oldFrame=frame;UpdateCapsules(frame);
        accumulator+=Mathf.Min(dt,.05f);const float h=1f/120;int steps=0;
        while(accumulator>=h && steps++<6)
        {
            Step(h,origin,frame,velocity,inertia);accumulator-=h;clock+=h;
        }
        Pin(origin,frame);WriteMesh();
    }
    void Step(float dt,Vector3 origin,Quaternion frame,Vector3 velocity,Vector3 inertia)
    {
        Vector3 wind=Finite(ambientWind)?Vector3.ClampMagnitude(ambientWind,20):new Vector3(2,.2f,-2);
        float gust=Safe(gustStrength,1.8f,0,8),drag=Safe(airDrag,1.8f,0,5),damping=dt*Mathf.Exp(-2.5f*dt);
        for(int i=Columns;i<ParticleCount;i++)
        {
            Vector3 p=positions[i],v=(p-previous[i])/dt;
            float phase=clock*3.1f+rest[i].x*8+rest[i].y*5;
            Vector3 flutter=new Vector3(Mathf.Sin(phase),Mathf.Sin(phase*1.37f)*.35f,Mathf.Cos(phase*.79f))*gust;
            // Porous cloth saturates its aerodynamic load; unbounded drag at
            // ODM speeds over-tensions the narrow collar instead of adding useful billow.
            Vector3 aero=Vector3.ClampMagnitude((wind+flutter-velocity-v)*drag,24);
            Vector3 acceleration=new Vector3(0,-9.8f,0)+aero+inertia*.15f;
            previous[i]=p;positions[i]=p+v*damping+acceleration*(dt*dt);
        }
        int iterations=Mathf.Clamp(solverIterations,4,16);
        for(int pass=0;pass<iterations;pass++)
        {
            Pin(origin,frame);
            for(int k=0;k<links.Length;k++)
            {
                var link=links[(pass&1)==0?k:links.Length-1-k];
                Vector3 d=positions[link.b]-positions[link.a];float length=d.magnitude;
                if(length<.000001f || (link.maximumOnly && length<=link.rest*scale))continue;
                float wa=link.a<Columns?0:1,wb=link.b<Columns?0:1;if(wa+wb==0)continue;
                Vector3 correction=d*((length-link.rest*scale)/length*link.stiffness/(wa+wb));
                if(wa>0)positions[link.a]+=correction;if(wb>0)positions[link.b]-=correction;
            }
            for(int i=Columns;i<ParticleCount;i++)
            {
                // Long-range attachment constraint prevents explosive stretching
                // after a discontinuous cosmetic pose; it never moves the player.
                Vector3 from=positions[i]-positions[i%Columns];float max=maximumReach[i];
                if(from.sqrMagnitude>max*max)positions[i]=positions[i%Columns]+from.normalized*max;
                ProjectBody(i);
            }
            // Finish with local material strain limits. Shear/bend/coarse-link
            // projections must not leave the narrow collar absorbing the whole
            // free panel's aerodynamic load as stretched first-row edges.
            foreach(var link in structuralLinks)
            {
                Vector3 d=positions[link.b]-positions[link.a];float length=d.magnitude,max=link.rest*scale*1.04f;
                if(length<=max)continue;
                float wa=link.a<Columns?0:1,wb=link.b<Columns?0:1;if(wa+wb==0)continue;
                Vector3 correction=d*((length-max)/length/(wa+wb));if(wa>0)positions[link.a]+=correction;if(wb>0)positions[link.b]-=correction;
            }
            for(int i=Columns;i<ParticleCount;i++)ProjectBody(i);
        }
        Pin(origin,frame);
    }
    void Pin(Vector3 origin,Quaternion frame)
    { for(int i=0;i<Columns;i++)positions[i]=previous[i]=origin+frame*rest[i]*scale; }
    void UpdateCapsules(Quaternion frame)
    {
        // Jacket cross-section is broad but shallow: a circular .18m proxy
        // falsely inflated the back/collar and overstrained the pinned fabric.
        capsules[0]=new Capsule{a=hips.position,b=chest.position,radius=.14f*scale,width=.19f*scale,rotation=frame};
        capsules[1]=new Capsule{a=leftThigh.position,b=leftKnee.position,radius=.095f*scale};
        capsules[2]=new Capsule{a=rightThigh.position,b=rightKnee.position,radius=.095f*scale};
        Vector3 up=(spine.position-hips.position).normalized,right=(rightThigh.position-leftThigh.position).normalized;
        Vector3 forward=Vector3.Cross(right,up).normalized*(reverseForward?-1:1),back=-forward;
        for(int j=0;j<2;j++)
        {
            Vector3 center=hips.position+right*((j==0?-1:1)*.245f*scale)+back*.153f*scale;
            capsules[3+j]=new Capsule{a=center-up*.12f*scale,b=center+up*.11f*scale,radius=.065f*scale};
        }
        Vector3 unit=hips.position+up*.10f*scale+back*.16f*scale;
        capsules[5]=new Capsule{a=unit,b=unit,radius=.12f*scale};
        capsules[6]=new Capsule{a=neck.position,b=neck.position+(neck.position-chest.position)*.5f,radius=.075f*scale};
        for(int i=0;i<capsules.Length;i++)
        {
            var cap=capsules[i];cap.axis=cap.b-cap.a;cap.inverseLength=cap.axis.sqrMagnitude>.000001f?1/cap.axis.sqrMagnitude:0;
            if(cap.width>0)cap.inverseRotation=Quaternion.Inverse(cap.rotation);
            cap.paddedRadius=cap.radius+.008f*scale;cap.paddedWidth=cap.width+.008f*scale;
            // Conservative outer sphere only rejects points that cannot enter
            // the elliptical torso. Margin protects the unchanged narrow-phase
            // calculation against floating-point boundary rounding.
            float broadRadius=Mathf.Max(cap.paddedRadius,cap.paddedWidth)+.0001f*scale;
            cap.broadRadiusSquared=broadRadius*broadRadius;
            Vector3 extent=Vector3.one*broadRadius;cap.min=Vector3.Min(cap.a,cap.b)-extent;cap.max=Vector3.Max(cap.a,cap.b)+extent;
            capsules[i]=cap;
        }
    }
    void ProjectBody(int i)
    {
        if(!bodyCollision)return;
        for(int k=0;k<capsules.Length;k++)
        {
            ref readonly Capsule cap=ref capsules[k];
            Vector3 point=positions[i];
            if(point.x<cap.min.x || point.x>cap.max.x || point.y<cap.min.y || point.y>cap.max.y || point.z<cap.min.z || point.z>cap.max.z)continue;
            float f=Mathf.Clamp01(Vector3.Dot(point-cap.a,cap.axis)*cap.inverseLength);
            Vector3 center=cap.a+cap.axis*f,d=positions[i]-center;float radius=cap.paddedRadius;
            if(cap.width>0)
            {
                if(d.sqrMagnitude>cap.broadRadiusSquared)continue;
                Vector3 local=cap.inverseRotation*d;float width=cap.paddedWidth;
                float normalized=Mathf.Sqrt(local.x*local.x/(width*width)+(local.y*local.y+local.z*local.z)/(radius*radius));
                if(normalized<1)positions[i]=center+(normalized>.00001f?cap.rotation*(local/normalized):-(oldFrame*Vector3.forward)*radius);
            }
            else if(d.sqrMagnitude<radius*radius)positions[i]=center+(d.sqrMagnitude>.0000001f?d.normalized:-(oldFrame*Vector3.forward))*radius;
        }
    }
    void WriteMesh()
    {
        if(mesh==null || fabric==null)return;
        Matrix4x4 toLocal=fabric.transform.worldToLocalMatrix;
        for(int row=0;row<Rows;row++)for(int col=0;col<Columns;col++)
        {
            int i=row*Columns+col;
            Vector3 horizontal=positions[row*Columns+Mathf.Min(col+1,Columns-1)]-positions[row*Columns+Mathf.Max(0,col-1)];
            Vector3 vertical=positions[Mathf.Min(row+1,Rows-1)*Columns+col]-positions[Mathf.Max(0,row-1)*Columns+col];
            Vector3 n=Vector3.Cross(horizontal,vertical).normalized;if(n.sqrMagnitude<.5f)n=-(oldFrame*Vector3.forward);normals[i]=n;
            Vector3 p=toLocal.MultiplyPoint3x4(positions[i]),localNormal=toLocal.MultiplyVector(n).normalized;
            Vector3 tangent=toLocal.MultiplyVector(horizontal.normalized).normalized;
            vertices[i]=vertices[ParticleCount+i]=p;renderNormals[i]=localNormal;renderNormals[ParticleCount+i]=-localNormal;
            tangents[i]=new Vector4(tangent.x,tangent.y,tangent.z,1);tangents[ParticleCount+i]=new Vector4(tangent.x,tangent.y,tangent.z,-1);
        }
        for(int i=0;i<emblem.Length;i++)
        {
            float f=Mathf.Clamp01((1.47f-emblem[i].y)/safeLength),u=Mathf.Clamp01(emblem[i].x/(2*Width(f))+.5f);
            float x=u*(Columns-1),y=f*(Rows-1);int c=Mathf.Min(Columns-2,(int)x),r=Mathf.Min(Rows-2,(int)y),a=r*Columns+c;
            Vector3 p=Vector3.Lerp(Vector3.Lerp(positions[a],positions[a+1],x-c),Vector3.Lerp(positions[a+Columns],positions[a+Columns+1],x-c),y-r);
            Vector3 n=Vector3.Lerp(Vector3.Lerp(normals[a],normals[a+1],x-c),Vector3.Lerp(normals[a+Columns],normals[a+Columns+1],x-c),y-r).normalized;
            int k=ParticleCount*2+i;vertices[k]=toLocal.MultiplyPoint3x4(p+n*.004f*scale);renderNormals[k]=toLocal.MultiplyVector(n).normalized;tangents[k]=tangents[a];
        }
        mesh.vertices=vertices;mesh.normals=renderNormals;mesh.tangents=tangents;mesh.RecalculateBounds();
    }
    public Vector3 GetParticle(int index) { return positions[index]; }
    public Vector3 GetPinTarget(int column) { return oldOrigin+oldFrame*rest[column]*scale; }
    public float MaximumStructuralStretch()
    {
        float max=1;foreach(var link in links)if(link.stiffness==1 && !link.maximumOnly)max=Mathf.Max(max,Vector3.Distance(positions[link.a],positions[link.b])/(link.rest*scale));return max;
    }
    public static void PreserveCorpseMesh(GameObject visualCopy,GameObject corpseRoot)
    {
        foreach(var filter in visualCopy.GetComponentsInChildren<MeshFilter>(true))
            if(filter.sharedMesh!=null && filter.sharedMesh.name==MeshName)
            {
                var snapshot=Instantiate(filter.sharedMesh);snapshot.name="Independent Scout corpse cape snapshot";filter.sharedMesh=snapshot;
                corpseRoot.AddComponent<ScoutCapeMeshLifetime>().mesh=snapshot;
            }
    }
    void OnEnable()
    {if(Initialized){original.enabled=false;if(renderer!=null)renderer.enabled=true;ResetSimulation();}}
    void OnDisable()
    {
        if(original!=null && (health==null || !health.isDead))original.enabled=originalEnabled;
        if(renderer!=null)renderer.enabled=false;
    }
    void OnDestroy()
    {if(fabric!=null)Destroy(fabric);if(mesh!=null)Destroy(mesh);}
}
