using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Independent death or recoverable-stomp physical copy of the real Scout skin/held pose.
/// No live skeleton physics, parenting into a Titan, or takeover of the motor root.
/// Safe-release callbacks can finish on the hidden player without moving this corpse.</summary>
[DisallowMultipleComponent]
public sealed class ScoutPlayerRagdoll : MonoBehaviour
{
    public Transform visualRoot;
    [Min(1)] public float corpseLifetime = 8;
    [Range(0,20)] public float maximumInheritedSpeed = 12;
    [Tooltip("Small outward release at a lethal chomp so the loose body slumps out of the jaw, not straight down through the Titan. Initial velocity only.")]
    [Range(0,6)] public float biteOutwardSpeed = 2.5f;
    [Tooltip("Initial chomp folding response in radians/second. Physics owns all motion afterwards; no animated corpse or post-simulation clipping.")]
    [Range(0,3)] public float biteTumble = 1.5f;
    public GameObject Corpse { get; private set; }
    public Rigidbody[] Bodies { get; private set; } = new Rigidbody[0];
    public string Error { get; private set; }
    public bool Temporary {get;private set;}
    public Transform Hips {get;private set;}
    Collider[] physicalShapes,ignoredAttacker;
    public int ActivationCount { get; private set; }
    public Vector3 CapturedPosition { get { return rootPosition; } }
    Vector3[] positions, scales;
    Quaternion[] rotations;
    Vector3 rootPosition, rootScale;
    Quaternion rootRotation;
    TitanBiteImpact bite;
    bool prepared,bitten,impactTumble;
    readonly Collider[] overlaps = new Collider[64];

    bool FindVisual()
    {
        if (visualRoot == null)
        {
            var driver = GetComponent<ScoutPlayerAnimation>();
            if (driver != null) visualRoot = driver.model;
        }
        return visualRoot != null && visualRoot.IsChildOf(transform);
    }
    public void PrepareBite(TitanBiteImpact impact)
    {
        positions = null; bite = impact; prepared = impact.Lethal;bitten=impact.Lethal;impactTumble=false;
        if (prepared) CaptureDeathPose();
    }
    public void PrepareHeldDeath(Transform attacker,Vector3 velocity)
    {
        positions=null;prepared=true;bitten=impactTumble=false;
        bite=new TitanBiteImpact{Attacker=attacker,Velocity=velocity};CaptureDeathPose();
    }
    public void PrepareImpactDeath(Transform attacker,Vector3 velocity)
    {PrepareHeldDeath(attacker,velocity);impactTumble=true;}
    public void PrepareNaturalDeath(Vector3 velocity,Transform holder=null)
    {
        if(prepared || Corpse!=null)return;prepared=true;bitten=false;impactTumble=holder==null;bite=new TitanBiteImpact{Velocity=velocity,Attacker=holder};CaptureDeathPose();
    }
    public void CompleteBite() { prepared = bitten = false; positions = null; }
    public bool CaptureDeathPose()
    {
        if (!FindVisual()) return false;
        // Preserve a lethal bite snapshot from BEFORE release/exit-search callbacks.
        if (prepared && positions != null) return true;
        var bones = visualRoot.GetComponentsInChildren<Transform>(true);
        positions = new Vector3[bones.Length]; rotations = new Quaternion[bones.Length]; scales = new Vector3[bones.Length];
        for (int i=0;i<bones.Length;i++)
        { positions[i]=bones[i].localPosition; rotations[i]=bones[i].localRotation; scales[i]=bones[i].localScale; }
        rootPosition=visualRoot.position;rootRotation=visualRoot.rotation;rootScale=visualRoot.lossyScale;
        return true;
    }
    public bool ActivateTemporary(Transform attacker,Vector3 velocity)
    {
        if(Corpse!=null)return false;PrepareHeldDeath(attacker,velocity);
        if(CreatePhysical(true))return true;
        // A refused live knockdown must not leave a stale pre-impact DEATH pose
        // armed for some later unrelated death after the player has moved away.
        prepared=bitten=impactTumble=false;positions=null;return false;
    }
    public void EndTemporary()
    {
        if(!Temporary)return;
        if(Corpse!=null){Corpse.SetActive(false);Destroy(Corpse);}Corpse=null;Bodies=new Rigidbody[0];Hips=null;
        Temporary=prepared=bitten=impactTumble=false;positions=null;physicalShapes=ignoredAttacker=null;
    }
    public void RestoreAttackerContactsWhenClear()
    {
        if(!Temporary || ignoredAttacker==null || physicalShapes==null)return;
        foreach(var shape in physicalShapes)foreach(var other in ignoredAttacker)
        {
            if(shape==null || other==null || !other.enabled || other.isTrigger || !other.gameObject.activeInHierarchy)continue;
            Vector3 normal;float depth;if(Physics.ComputePenetration(shape,shape.transform.position,shape.transform.rotation,other,other.transform.position,other.transform.rotation,out normal,out depth)&&depth>.001f)return;
        }
        foreach(var shape in physicalShapes)foreach(var other in ignoredAttacker)if(shape!=null&&other!=null)Physics.IgnoreCollision(shape,other,false);
        ignoredAttacker=null;
    }
    public bool Activate()
    {
        if(Corpse!=null){if(Temporary){Temporary=false;var proxy=Corpse.GetComponent<TitanCombatProxy>();if(proxy!=null)proxy.target=null;Destroy(Corpse,SafeLifetime());}return true;}
        return CreatePhysical(false);
    }
    internal void ResetForRespawn()
    {
        // Independent permanent corpse keeps its original bounded lifetime. It
        // MUST NOT keep a temporary proxy pointing at the revived real player.
        if(Corpse!=null){var proxy=Corpse.GetComponent<TitanCombatProxy>();if(proxy!=null)proxy.target=null;if(Temporary){Corpse.SetActive(false);Destroy(Corpse);}}
        Corpse=null;Bodies=new Rigidbody[0];Hips=null;Temporary=prepared=bitten=impactTumble=false;
        positions=null;physicalShapes=ignoredAttacker=null;Error=null;
    }
    float SafeLifetime(){return float.IsNaN(corpseLifetime)||float.IsInfinity(corpseLifetime)?8:Mathf.Clamp(corpseLifetime,1,30);}
    bool CreatePhysical(bool temporary)
    {
        if (!FindVisual() || positions == null) { Error="No captured Scout visual pose";return false; }
        // The approved Scout visual is passive. Never clone/take over foreign physics.
        if (visualRoot.GetComponentsInChildren<Rigidbody>(true).Length != 0 || visualRoot.GetComponentsInChildren<Collider>(true).Length != 0)
        { Error="Scout visual already has external physics; refusing duplicate ragdoll ownership";return false; }
        GameObject root = new GameObject("[Scout corpse] Physical bite/death pose");
        root.SetActive(false);root.hideFlags=HideFlags.DontSave;SceneManager.MoveGameObjectToScene(root,gameObject.scene);
        try
        {
            var copy=Instantiate(visualRoot.gameObject,root.transform,false);
            foreach(var a in copy.GetComponentsInChildren<Animator>(true))a.enabled=false;
            foreach(var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) {script.enabled=false;Destroy(script);}
            foreach(var audio in copy.GetComponentsInChildren<AudioSource>(true)) {audio.playOnAwake=false;audio.enabled=false;}
            foreach(var trail in copy.GetComponentsInChildren<TrailRenderer>(true)){trail.emitting=false;trail.Clear();trail.enabled=false;}
            var bones=copy.GetComponentsInChildren<Transform>(true);
            if(bones.Length!=positions.Length)throw new InvalidOperationException("Scout hierarchy changed during death callback");
            copy.transform.SetPositionAndRotation(rootPosition,rootRotation);copy.transform.localScale=rootScale;
            for(int i=1;i<bones.Length;i++) {bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];bones[i].localScale=scales[i];}
            ScoutCapeCloth.PreserveCorpseMesh(copy,root);
            foreach(var skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
            var named=new Dictionary<string,Transform>();foreach(var bone in bones)if(!named.ContainsKey(bone.name))named.Add(bone.name,bone);
            var rigid=new Dictionary<string,Rigidbody>();var shapes=new List<Collider>();
            float size=Mathf.Abs(rootScale.y);
            AddBody(named,rigid,shapes,"Hips","Spine",.13f,12,size);
            AddBody(named,rigid,shapes,"Chest","Neck",.16f,20,size);
            AddBody(named,rigid,shapes,"Head",null,.13f,6,size);
            foreach(string side in new[]{"Left","Right"})
            {
                AddBody(named,rigid,shapes,side+"UpperArm",side+"LowerArm",.06f,3,size);
                AddBody(named,rigid,shapes,side+"LowerArm",side+"Hand",.05f,2,size);
                AddBody(named,rigid,shapes,side+"UpperLeg",side+"LowerLeg",.08f,8,size);
                AddBody(named,rigid,shapes,side+"LowerLeg",side+"Foot",.065f,5,size);
                AddBody(named,rigid,shapes,side+"Foot",side+"Toes",.055f,1,size);
            }
            // Initialize native RB frames before creating joints: inactive new
            // rigidbodies can still report the origin, NOT the captured bone pose.
            root.SetActive(true);
            foreach(var rb in rigid.Values){rb.position=rb.transform.position;rb.rotation=rb.transform.rotation;}
            Physics.SyncTransforms();
            foreach(var pair in rigid)
            {
                if(pair.Key=="Hips")continue;
                string parent=pair.Key=="Chest"||pair.Key.Contains("UpperLeg")?"Hips":
                    pair.Key=="Head"||pair.Key.Contains("UpperArm")?"Chest":
                    pair.Key.Replace("LowerArm","UpperArm").Replace("LowerLeg","UpperLeg").Replace("Foot","LowerLeg");
                var rb=pair.Value;var joint=rb.gameObject.AddComponent<CharacterJoint>();joint.connectedBody=rigid[parent];
                joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;
                joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(rb.transform.position);
                joint.axis=Vector3.right;joint.swingAxis=Vector3.forward;
                joint.lowTwistLimit=new SoftJointLimit{limit=-25};joint.highTwistLimit=new SoftJointLimit{limit=25};
                joint.swing1Limit=new SoftJointLimit{limit=pair.Key=="Head"?30:55};joint.swing2Limit=new SoftJointLimit{limit=25};
                joint.enableCollision=false;joint.enablePreprocessing=false;joint.enableProjection=true;
                joint.projectionDistance=.015f;joint.projectionAngle=8;
            }
            // No self/catching-Titan contact explosions when the corpse starts
            // between the teeth. World/other Titans/corpses still collide normally.
            Collider[] attacker=prepared && bite.Attacker!=null?bite.Attacker.GetComponentsInChildren<Collider>(true):new Collider[0];
            for(int i=0;i<shapes.Count;i++)
            {
                for(int j=0;j<i;j++)Physics.IgnoreCollision(shapes[i],shapes[j],true);
                foreach(var c in attacker)if(c!=null)Physics.IgnoreCollision(shapes[i],c,true);
            }
            if(!WorldIsClear(shapes,root.transform,prepared?bite.Attacker:null))throw new InvalidOperationException("Scout corpse pose intersects solid scenery; refusing unsafe dynamic handoff");
            Vector3 velocity=prepared?bite.Velocity:Vector3.zero;
            if(!Finite(velocity))velocity=Vector3.zero;
            float limit=float.IsNaN(maximumInheritedSpeed)||float.IsInfinity(maximumInheritedSpeed)?12:Mathf.Clamp(maximumInheritedSpeed,0,20);
            velocity=Vector3.ClampMagnitude(velocity,limit); // Bounded input handoff, NEVER post-simulation clipping.
            float outward=float.IsNaN(biteOutwardSpeed)||float.IsInfinity(biteOutwardSpeed)?2.5f:Mathf.Clamp(biteOutwardSpeed,0,6);
            if(prepared && Finite(bite.Direction) && bite.Direction.sqrMagnitude>.0001f)velocity+=bite.Direction.normalized*outward;
            Bodies=new Rigidbody[rigid.Count];rigid.Values.CopyTo(Bodies,0);
            float tumble=float.IsNaN(biteTumble)||float.IsInfinity(biteTumble)?1.5f:Mathf.Clamp(biteTumble,0,3);
            Vector3 right=prepared && bite.Direction.sqrMagnitude>.0001f?Vector3.Cross(Vector3.up,bite.Direction).normalized:Vector3.right;
            foreach(var rb in Bodies)
            {
                rb.position=rb.transform.position;rb.rotation=rb.transform.rotation;rb.isKinematic=false;rb.detectCollisions=true;rb.useGravity=true;rb.velocity=temporary?Vector3.zero:velocity;if(temporary)rb.AddForce(velocity,ForceMode.VelocityChange);
                // Uniform gravity alone leaves a floating ragdoll rigid-looking.
                // Bounded opposite chest/hip impulses make the bitten body fold.
                float bend=rb.name=="Chest"?1:rb.name=="Head"?-.8f:rb.name=="Hips"?-.65f:rb.name.Contains("LowerLeg")?.65f:-.35f;
                Vector3 fallAxis=Vector3.Cross(Vector3.up,velocity).normalized;if(fallAxis.sqrMagnitude<.5f)fallAxis=rootRotation*Vector3.right;
                rb.angularVelocity=temporary||impactTumble?fallAxis*((temporary?2.1f:1.4f)+.35f*bend):prepared && bitten?right*(tumble*bend):Vector3.zero;
                if(temporary||impactTumble)
                {
                    // Coherent whole-body angular momentum, not isolated spins
                    // that the joints immediately cancel. Birth impulse ONLY.
                    Vector3 tipping=Vector3.Cross(fallAxis*(temporary?2.1f:1.4f),rb.worldCenterOfMass-rigid["Hips"].worldCenterOfMass);
                    Vector3 launch=Vector3.ClampMagnitude(velocity+tipping,18);
                    if(temporary)rb.AddForce(launch-velocity,ForceMode.VelocityChange);else rb.velocity=launch;
                }
                rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            }
            Corpse=root;Temporary=temporary;Hips=rigid["Hips"].transform;physicalShapes=shapes.ToArray();ignoredAttacker=temporary?attacker:null;ActivationCount++;Error=null;
            var driver=GetComponent<ScoutPlayerAnimation>();if(driver!=null)driver.enabled=false;
            HideOriginal();
            if(!temporary)Destroy(root,SafeLifetime());return true;
        }
        catch(Exception e){Error=e.Message;root.SetActive(false);Destroy(root);Corpse=null;Temporary=false;Hips=null;physicalShapes=ignoredAttacker=null;Bodies=new Rigidbody[0];return false;}
    }
    static bool Finite(Vector3 v){return !(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z));}
    static void AddBody(Dictionary<string,Transform> bones,Dictionary<string,Rigidbody> bodies,List<Collider> shapes,string name,string end,float radius,float mass,float size)
    {
        if(!bones.ContainsKey(name)||(end!=null&&!bones.ContainsKey(end)))throw new InvalidOperationException("Missing Scout ragdoll bone "+name+" / "+end);
        var bone=bones[name];var rb=bone.gameObject.AddComponent<Rigidbody>();rb.isKinematic=true;rb.detectCollisions=false;rb.useGravity=false;
        rb.mass=mass;rb.solverIterations=12;rb.solverVelocityIterations=4;rb.maxAngularVelocity=4;rb.interpolation=RigidbodyInterpolation.None;
        bodies.Add(name,rb);
        var go=new GameObject("Corpse shape "+name);go.layer=bone.gameObject.layer;go.transform.SetParent(bone,false);
        Vector3 s=bone.lossyScale;go.transform.localScale=new Vector3(1/Mathf.Abs(s.x),1/Mathf.Abs(s.y),1/Mathf.Abs(s.z));
        if(end==null)
        {
            var sphere=go.AddComponent<SphereCollider>();sphere.center=go.transform.InverseTransformPoint(bone.position+bone.up*.09f*size);sphere.radius=radius*size;shapes.Add(sphere);
        }
        else
        {
            Vector3 delta=bones[end].position-bone.position;
            if(delta.sqrMagnitude<.000001f)throw new InvalidOperationException("Zero length Scout ragdoll segment "+name);
            go.transform.rotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);
            var capsule=go.AddComponent<CapsuleCollider>();capsule.direction=1;capsule.radius=radius*size;
            capsule.height=Mathf.Max(delta.magnitude,2*capsule.radius);capsule.center=go.transform.InverseTransformPoint((bone.position+bones[end].position)*.5f);shapes.Add(capsule);
        }
    }
    bool WorldIsClear(List<Collider> shapes,Transform own,Transform attacker)
    {
        var physics=gameObject.scene.GetPhysicsScene();
        foreach(var shape in shapes)
        {
            int n;
            var capsule=shape as CapsuleCollider;
            if(capsule!=null)
            {
                float half=Mathf.Max(0,capsule.height*.5f-capsule.radius);Vector3 center=capsule.transform.TransformPoint(capsule.center),axis=capsule.transform.up*half;
                n=physics.OverlapCapsule(center-axis,center+axis,capsule.radius,overlaps,~0,QueryTriggerInteraction.Ignore);
            }
            else
            {
                var sphere=(SphereCollider)shape;
                n=physics.OverlapSphere(sphere.transform.TransformPoint(sphere.center),sphere.radius,overlaps,~0,QueryTriggerInteraction.Ignore);
            }
            if(n>=overlaps.Length)return false;
            for(int i=0;i<n;i++)
            {
                var other=overlaps[i];if(other==null||other.transform.IsChildOf(own)||other.transform.IsChildOf(transform)||(attacker!=null&&other.transform.IsChildOf(attacker)))continue;
                Vector3 direction;float distance;
                if(Physics.ComputePenetration(shape,shape.transform.position,shape.transform.rotation,other,other.transform.position,other.transform.rotation,out direction,out distance)&&distance>.005f)return false;
            }
        }
        return true;
    }
    void HideOriginal()
    {
        if(visualRoot!=null)foreach(var r in visualRoot.GetComponentsInChildren<Renderer>(true))r.enabled=false;
        foreach(var a in GetComponentsInChildren<Animator>(true))a.enabled=false;
        foreach(var c in GetComponentsInChildren<Collider>(true))c.enabled=false;
        foreach(var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            if(!rb.isKinematic){rb.velocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}
            rb.isKinematic=true;rb.detectCollisions=false;rb.useGravity=false;
        }
    }
    void LateUpdate(){if(Corpse!=null)HideOriginal();}
    void OnDestroy(){if(Corpse!=null)Destroy(Corpse);}
}
