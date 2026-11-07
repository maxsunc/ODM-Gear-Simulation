using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>New-life reset on the SAME player root. Scene actors, references, music
/// and spawner clocks survive. Only a dead player can teleport to a clear birth.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerRespawn : MonoBehaviour
{
    public const float Delay=3;
    public Vector3 SpawnPosition {get;private set;}
    public Quaternion SpawnRotation {get;private set;}
    public int RespawnCount {get;private set;}
    public bool Pending {get;private set;}
    public string Error {get;private set;}
    public float Remaining {get{return Pending?Mathf.Max(0,readyAt-Time.time):0;}}
    PlayerHealth health;Transform originalParent;Vector3 originalScale;
    bool initialized,allowMovement;float readyAt;
    struct Node {public Transform t;public Vector3 position,scale;public Quaternion rotation;public bool active;}
    struct Collision {public Collider c;public bool enabled,active;}
    struct Body {public Rigidbody rb;public bool kinematic,gravity,collisions;public CollisionDetectionMode detection;}
    readonly List<Node> nodes=new List<Node>();
    readonly Dictionary<Renderer,bool> renderers=new Dictionary<Renderer,bool>();
    readonly Dictionary<Behaviour,bool> scripts=new Dictionary<Behaviour,bool>();
    readonly List<Collision> collisions=new List<Collision>();readonly List<Body> bodies=new List<Body>();
    readonly RaycastHit[] supports=new RaycastHit[64];
    public void Initialize()
    {
        if(initialized)return;initialized=true;health=GetComponent<PlayerHealth>();
        SpawnPosition=transform.position;SpawnRotation=transform.rotation;originalScale=transform.localScale;originalParent=transform.parent;
        var walk=GetComponent<Movement>();allowMovement=walk!=null&&walk.enabledMovement;
        CaptureLivingState();
    }
    public void CaptureLivingState()
    {
        if(!initialized){Initialize();return;}
        var target=GetComponent<TitanCombatTarget>();var motor=GetComponent<TitanODMMotor>();var reaction=GetComponent<ScoutStompReaction>();
        if(health.isDead||health.isGrabbed||(target!=null&&(target.IsHeld||target.ReleasePending))||(motor!=null&&motor.Suspended)||(reaction!=null&&reaction.IsActive))return;
        // First-seen local poses/active states form a pristine hierarchy. Refresh
        // enable flags only while actually alive/free, never from a hidden hold.
        foreach(var t in GetComponentsInChildren<Transform>(true))
        {
            if(t==transform||nodes.Exists(n=>n.t==t))continue;
            nodes.Add(new Node{t=t,position=t.localPosition,rotation=t.localRotation,scale=t.localScale,active=t.gameObject.activeSelf});
        }
        foreach(var r in GetComponentsInChildren<Renderer>(true))renderers[r]=r.enabled;
        foreach(var s in GetComponentsInChildren<Behaviour>(true))scripts[s]=s.enabled;
        collisions.Clear();foreach(var c in GetComponentsInChildren<Collider>(true))collisions.Add(new Collision{c=c,enabled=c.enabled,active=c.gameObject.activeInHierarchy});
        bodies.Clear();foreach(var rb in GetComponentsInChildren<Rigidbody>(true))bodies.Add(new Body{rb=rb,kinematic=rb.isKinematic,gravity=rb.useGravity,collisions=rb.detectCollisions,detection=rb.collisionDetectionMode});
        // Keep authored walking permission, not the temporary enabledMovement=false
        // set by ordinary powered flight/inertia. New lives must not inherit that lock.
    }
    public void BeginDeath()
    {Initialize();Pending=true;readyAt=Time.time+Delay;Error=null;}
    void RestorePose(bool activeStates)
    {
        foreach(var n in nodes)if(n.t!=null){n.t.localPosition=n.position;n.t.localRotation=n.rotation;n.t.localScale=n.scale;if(activeStates&&n.t.gameObject.activeSelf!=n.active)n.t.gameObject.SetActive(n.active);}
    }
    bool FindSpawn(out Vector3 point)
    {
        point=SpawnPosition;
        if(!TitanSpawnPlacement.Finite(SpawnPosition)||!TitanSpawnPlacement.Finite(originalScale)||originalScale.x<=0||originalScale.y<=0||originalScale.z<=0)
        {Error="Invalid authored spawn pose; respawn waits";return false;}
        var solids=new List<Collider>();foreach(var c in collisions)if(c.c!=null&&c.enabled&&c.active&&!c.c.isTrigger)
        {if(!TitanReleaseClearance.Supported(c.c)){Error="Unsupported solid player shape; respawn waits";return false;}solids.Add(c.c);}
        if(solids.Count==0){Error="No configured solid player shape; respawn waits";return false;}
        // Query the pristine pose atomically with ALL live colliders disabled.
        // No frame, simulation, controls or rendering observes this probe; restore
        // the hidden death pose even on a rejected birth. Corpses are NOT ignored.
        Transform parent=transform.parent;Vector3 pos=transform.localPosition,scale=transform.localScale;Quaternion rot=transform.localRotation;
        var saved=new Node[nodes.Count];for(int i=0;i<nodes.Count;i++){var t=nodes[i].t;if(t!=null)saved[i]=new Node{t=t,position=t.localPosition,rotation=t.localRotation,scale=t.localScale};}
        try
        {
            transform.SetParent(originalParent,true);transform.localScale=originalScale;transform.SetPositionAndRotation(SpawnPosition,SpawnRotation);RestorePose(false);
            var clearance=new TitanReleaseClearance(transform,solids.ToArray());Physics.SyncTransforms();
            if(clearance.CanStandAt(point,null))return true;
            var cc=GetComponent<CharacterController>();float lift=cc!=null?cc.skinWidth+.03f:.12f;
            point=SpawnPosition+Vector3.up*lift;if(clearance.CanStandAt(point,null))return true;
            // A corpse/Titan may occupy the start. Bounded nearby GROUNDED births,
            // not arbitrary height searches, no gameplay RNG and no unsafe fallback.
            if(cc!=null)for(int ring=1;ring<=3;ring++)for(int side=0;side<8;side++)
            {
                float angle=side*Mathf.PI/4;Vector3 probe=SpawnPosition+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring*4);
                int count=gameObject.scene.GetPhysicsScene().Raycast(probe+Vector3.up*3,Vector3.down,supports,12,~0,QueryTriggerInteraction.Ignore);
                if(count>=supports.Length)continue;float highest=float.NegativeInfinity;Vector3 floor=Vector3.zero;
                float maximumFloor=SpawnPosition.y+transform.TransformVector(cc.center).y-cc.height*Mathf.Abs(transform.lossyScale.y)*.5f+cc.stepOffset+.12f;
                for(int i=0;i<count;i++){var h=supports[i];if(h.collider==null||h.rigidbody!=null||h.transform.IsChildOf(transform)||h.point.y>maximumFloor||h.normal.y<Mathf.Cos(cc.slopeLimit*Mathf.Deg2Rad))continue;if(h.point.y>highest){highest=h.point.y;floor=h.point;}}
                if(float.IsNegativeInfinity(highest))continue;
                point=floor+Vector3.up*(cc.height*Mathf.Abs(transform.lossyScale.y)*.5f+lift)-transform.TransformVector(cc.center);
                if(clearance.CanStandAt(point,null))return true;
            }
            Error="Spawn and nearby supported positions blocked; waiting for full player clearance";return false;
        }
        finally
        {
            foreach(var n in saved)if(n.t!=null){n.t.localPosition=n.position;n.t.localRotation=n.rotation;n.t.localScale=n.scale;}
            transform.SetParent(parent,false);transform.localPosition=pos;transform.localRotation=rot;transform.localScale=scale;Physics.SyncTransforms();
        }
    }
    public bool TryRespawn()
    {
        if(!Pending||health==null||!health.isDead||Time.timeScale<=0||Time.time+.001f<readyAt)return false;
        // Disable every live solid before probing/teleporting; never move a live CCT.
        foreach(var c in GetComponentsInChildren<Collider>(true))c.enabled=false;
        foreach(var rb in GetComponentsInChildren<Rigidbody>(true)){if(!rb.isKinematic){rb.velocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}rb.isKinematic=true;rb.detectCollisions=false;rb.useGravity=false;}
        Vector3 point;if(!FindSpawn(out point))return false;
        var target=GetComponent<TitanCombatTarget>();if(target!=null&&!target.ResetForRespawn())return false;
        var reaction=GetComponent<ScoutStompReaction>();if(reaction!=null)reaction.ResetForRespawn();
        var motor=GetComponent<TitanODMMotor>();if(motor!=null)motor.ResetForRespawn();
        var walk=GetComponent<Movement>();if(walk!=null)walk.ResetForRespawn(allowMovement);
        var gear=GetComponent<ODM_Movement>();if(gear!=null)gear.ResetForRespawn();
        var attack=GetComponent<Attack>();if(attack!=null)attack.ResetForRespawn();
        var escape=GetComponent<TitanGrabEscape>();if(escape!=null)escape.ResetForRespawn();
        var ragdoll=GetComponent<ScoutPlayerRagdoll>();if(ragdoll!=null)ragdoll.ResetForRespawn();
        var legacy=GetComponent<RagdollController>();if(legacy!=null)legacy.DisableRagdoll();
        transform.SetParent(originalParent,true);transform.localScale=originalScale;transform.SetPositionAndRotation(point,SpawnRotation);RestorePose(true);
        foreach(var b in bodies)if(b.rb!=null){b.rb.position=b.rb.transform.position;b.rb.rotation=b.rb.transform.rotation;b.rb.isKinematic=b.kinematic;b.rb.useGravity=b.gravity;b.rb.collisionDetectionMode=b.detection;b.rb.detectCollisions=b.collisions;if(!b.rb.isKinematic){b.rb.velocity=Vector3.zero;b.rb.angularVelocity=Vector3.zero;}}
        foreach(var entry in renderers)if(entry.Key!=null&&!(entry.Key is LineRenderer)&&!(entry.Key is TrailRenderer))entry.Key.enabled=entry.Value;
        foreach(var a in GetComponentsInChildren<Animator>(true))if(scripts.ContainsKey(a)&&scripts[a]&&a.gameObject.activeInHierarchy&&a.runtimeAnimatorController!=null){a.enabled=true;a.Rebind();a.Update(0);}
        var driver=GetComponent<ScoutPlayerAnimation>();if(driver!=null)driver.ResetMotionHistory();
        var cloth=GetComponent<ScoutCapeCloth>();if(cloth!=null&&cloth.Initialized)cloth.ResetSimulation();
        var feedback=GetComponent<PlayerDeathFeedback>();if(feedback!=null)feedback.ResetForRespawn();
        var sword=GetComponent<TitanSwordFeedback>();if(sword!=null)sword.ResetForRespawn();
        var wind=GetComponent<PlayerWindAudio>();if(wind!=null)wind.ResetMotionHistory();
        foreach(var p in GetComponentsInChildren<ParticleSystem>(true))p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach(var t in GetComponentsInChildren<TrailRenderer>(true)){t.emitting=false;t.Clear();}
        health.CompleteRespawn();
        foreach(var entry in scripts)if(entry.Key!=null)entry.Key.enabled=entry.Value;
        foreach(var c in collisions)if(c.c!=null)c.c.enabled=c.enabled;
        if(attack!=null&&attack.damageDealer!=null)attack.damageDealer.gameObject.SetActive(false);
        if(motor!=null)motor.ResetMotionHistory();Physics.SyncTransforms();
        foreach(var follow in FindObjectsOfType<FollowPos>())if(follow.isActiveAndEnabled&&follow.gameObject.scene==gameObject.scene&&follow.target!=null&&follow.target.IsChildOf(transform))
        {follow.transform.position=follow.target.position+follow.offset;foreach(var fix in follow.GetComponentsInChildren<CameraJitterFix>(true))fix.ResetAfterTeleport();}
        foreach(var camera in FindObjectsOfType<ThirdPersonCameraController>())if(camera.isActiveAndEnabled&&camera.gameObject.scene==gameObject.scene&&camera.target!=null&&camera.target.IsChildOf(transform))
        {camera.ResetAfterTeleport();foreach(var fix in camera.GetComponentsInChildren<CameraJitterFix>(true))fix.ResetAfterTeleport();}
        foreach(var fix in GetComponentsInChildren<CameraJitterFix>(true))fix.ResetAfterTeleport();
        Pending=false;Error=null;RespawnCount++;return true;
    }
}
