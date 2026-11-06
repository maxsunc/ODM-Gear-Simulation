using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Cosmetic impact observer. Four scene-owned reusable bursts, no physics
/// bodies/colliders/lights, no attack timers, no gameplay RNG or root writer.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TitanStompAttack))]
public sealed class TitanStompDebris : MonoBehaviour
{
    const int Slots=4;
    public int BurstCount {get;private set;}
    public Vector3 LastPoint {get;private set;}
    public int LastParticleCount {get;private set;}
    TitanStompAttack stomp;
    System.Random random;
    sealed class Pool
    {
        public GameObject root;
        public Mesh mesh;
        public readonly ParticleSystem[] dust=new ParticleSystem[Slots],chunks=new ParticleSystem[Slots];
        public int next;
        public void Dispose(){if(root!=null)Destroy(root);if(mesh!=null)Destroy(mesh);}
    }
    static readonly Dictionary<int,Pool> pools=new Dictionary<int,Pool>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPools()
    {
        SceneManager.sceneUnloaded-=OnSceneUnloaded;
        foreach(var p in pools.Values)p.Dispose();pools.Clear();SceneManager.sceneUnloaded+=OnSceneUnloaded;
    }
    static void OnSceneUnloaded(Scene scene)
    {Pool pool;if(pools.TryGetValue(scene.handle,out pool)){pool.Dispose();pools.Remove(scene.handle);}}
    public void ResetVariation(int seed){random=new System.Random(seed);}
    void Awake(){stomp=GetComponent<TitanStompAttack>();ResetVariation(Environment.TickCount^GetInstanceID());}
    void OnEnable(){stomp.Impacted+=OnImpact;}
    void OnDisable(){if(stomp!=null)stomp.Impacted-=OnImpact;}
    public static int PoolParticleSystemCount(Scene scene)
    {Pool pool;return pools.TryGetValue(scene.handle,out pool)&&pool.root!=null?pool.root.GetComponentsInChildren<ParticleSystem>().Length:0;}
    void OnImpact(Vector3 point,bool left)
    {
        if(!isActiveAndEnabled || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)return;
        Pool pool;if(!pools.TryGetValue(gameObject.scene.handle,out pool) || pool.root==null)
        {
            var dustMaterial=Resources.Load<Material>("TitanStompDust");var chunkMaterial=Resources.Load<Material>("TitanStompChunks");
            if(dustMaterial==null || chunkMaterial==null)return;
            pool=new Pool();pool.root=new GameObject("[Titan FX] Stomp debris pool");pool.root.hideFlags=HideFlags.DontSave;pool.root.layer=2;
            SceneManager.MoveGameObjectToScene(pool.root,gameObject.scene);pool.mesh=RockMesh();
            for(int i=0;i<Slots;i++){pool.dust[i]=Create(pool.root,"Dust "+i,dustMaterial,null,80);pool.chunks[i]=Create(pool.root,"Rock fragments "+i,chunkMaterial,pool.mesh,32);}
            pools[gameObject.scene.handle]=pool;
        }
        int slot=pool.next++%Slots;float size=Mathf.Clamp(stomp.GeometryScale,.25f,10),energy=Mathf.Sqrt(size);
        float spread=Mathf.Max(.1f,stomp.impactRadius)*size*.68f;
        int dustCount=Mathf.Clamp(Mathf.RoundToInt(40+8*energy),40,72),chunkCount=Mathf.Clamp(Mathf.RoundToInt(14+5*energy),16,30);
        Emit(pool.dust[slot],point,spread,energy,dustCount,false);Emit(pool.chunks[slot],point,spread*.8f,energy,chunkCount,true);
        LastPoint=point;LastParticleCount=dustCount+chunkCount;BurstCount++;
    }
    static ParticleSystem Create(GameObject root,string name,Material material,Mesh mesh,int capacity)
    {
        var go=new GameObject(name);go.layer=2;go.transform.SetParent(root.transform,false);
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=1;
        var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=2.2f;main.maxParticles=capacity;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.gravityModifier=mesh!=null?1:.12f;main.startSpeed=0;main.useUnscaledTime=false;
        var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
        var rotation=ps.rotationOverLifetime;rotation.enabled=mesh!=null;rotation.separateAxes=true;
        rotation.x=new ParticleSystem.MinMaxCurve(-3,3);rotation.y=new ParticleSystem.MinMaxCurve(-4,4);rotation.z=new ParticleSystem.MinMaxCurve(-3,3);
        var color=ps.colorOverLifetime;color.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(mesh!=null?1:.75f,0),new GradientAlphaKey(mesh!=null?1:.5f,.45f),new GradientAlphaKey(0,1)});color.color=gradient;
        var growth=ps.sizeOverLifetime;growth.enabled=mesh==null;growth.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.7f),new Keyframe(1,1.8f)));
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.renderMode=mesh!=null?ParticleSystemRenderMode.Mesh:ParticleSystemRenderMode.Billboard;if(mesh!=null)renderer.mesh=mesh;
        return ps;
    }
    void Emit(ParticleSystem ps,Vector3 point,float spread,float energy,int count,bool chunks)
    {
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.randomSeed=(uint)random.Next(1,int.MaxValue);ps.Play();
        for(int i=0;i<count;i++)
        {
            float angle=Range(0,Mathf.PI*2),radius=Mathf.Sqrt(Range(0,1))*spread;var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var p=new ParticleSystem.EmitParams{position=point+radial*radius+Vector3.up*.15f*energy,
                velocity=radial*Range(chunks?2:1,chunks?5:3)*energy+Vector3.up*Range(chunks?3:1.2f,chunks?7:3)*energy,
                startLifetime=Range(chunks?.7f:1.2f,chunks?1.5f:2),startSize=Range(chunks?.09f:.8f,chunks?.22f:1.4f)*energy,
                startColor=chunks?Color.Lerp(new Color(.22f,.18f,.13f),new Color(.48f,.40f,.29f),Range(0,1)):new Color(.48f,.40f,.29f,.75f),rotation3D=chunks?new Vector3(Range(0,360),Range(0,360),Range(0,360)):new Vector3(0,0,Range(0,360))};
            ps.Emit(p,1);
        }
    }
    float Range(float a,float b){return Mathf.Lerp(a,b,(float)random.NextDouble());}
    static Mesh RockMesh()
    {
        var corners=new[]{new Vector3(-.5f,-.35f,-.4f),new Vector3(.45f,-.4f,-.45f),new Vector3(.4f,-.3f,.45f),new Vector3(-.4f,-.4f,.4f),new Vector3(-.3f,.45f,-.3f),new Vector3(.3f,.4f,-.35f),new Vector3(.25f,.5f,.3f),new Vector3(-.35f,.35f,.25f)};
        var faces=new[]{0,3,2,1,4,5,6,7,0,1,5,4,1,2,6,5,2,3,7,6,3,0,4,7};
        var vertices=new Vector3[24];var uv=new Vector2[24];var triangles=new int[36];
        for(int face=0;face<6;face++)
        {
            for(int j=0;j<4;j++){vertices[face*4+j]=corners[faces[face*4+j]];uv[face*4+j]=new Vector2(j==1||j==2?1:0,j>=2?1:0);}
            int v=face*4,t=face*6;triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;triangles[t+3]=v;triangles[t+4]=v+3;triangles[t+5]=v+2;
        }
        var mesh=new Mesh{name="Stomp faceted rock",hideFlags=HideFlags.DontSave};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
}
