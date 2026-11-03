using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit AIRBORNE LAB proxy, not the real ODM adapter. Straight boosted
/// pass before capture; on release gravity resumes with decayed incoming + hand
/// momentum. Full capsule clearance; blocked releases retry without Titan ownership.</summary>
[RequireComponent(typeof(CharacterController))]
public class TitanSandboxFlight : TitanAerialMotor
{
    public bool automaticTick=true;
    public bool IsFlying { get; private set; }
    public bool Suspended { get; private set; }
    public bool boostedPass;
    Vector3 velocity, incoming, measured;
    int launchFrame=-1;
    CharacterController controller;
    TitanCombatTarget health;
    readonly Collider[] overlaps=new Collider[64];
    readonly RaycastHit[] hits=new RaycastHit[64];
    public override Vector3 Velocity { get { return measured; } }
    public override bool CanSuspend { get { return isActiveAndEnabled && IsFlying && !Suspended && controller!=null && controller.enabled; } }
    void Awake() { controller=GetComponent<CharacterController>(); health=GetComponent<TitanCombatTarget>(); }
    public void Launch(Vector3 position, Vector3 speed)
    {
        if(!Finite(position) || !Finite(speed)) return;
        if(health!=null) health.EscapeHold();
        if(health!=null && health.ReleasePending) return;
        bool enabled=controller.enabled; controller.enabled=false; transform.position=position; controller.enabled=enabled;
        velocity=measured=speed; IsFlying=true; boostedPass=true; launchFrame=Time.frameCount;
    }
    public void StopFlight() { if(!Suspended) { IsFlying=false; velocity=measured=Vector3.zero; } }
    void Update() { if(automaticTick && Time.frameCount!=launchFrame) TickFlight(Time.deltaTime); }
    public void TickFlight(float dt)
    {
        if(health==null) health=GetComponent<TitanCombatTarget>();
        if(health!=null && health.ReleasePending && !health.enabled) health.TickHold(0);
        if(dt<0 || float.IsNaN(dt) || float.IsInfinity(dt) || !IsFlying || Suspended || controller==null || !controller.enabled) return;
        if(!boostedPass) velocity+=Physics.gravity*dt;
        Vector3 before=transform.position;
        controller.Move(velocity*dt);
        if(dt>0) measured=(transform.position-before)/dt;
        if(!boostedPass && controller.isGrounded && velocity.y<0) StopFlight();
    }
    public override bool TrySuspend()
    {
        if(!CanSuspend) return false;
        // This adapter deliberately supports one upright controller, no compound solids.
        if(Vector3.Dot(transform.up,Vector3.up)<.9999f) return false;
        foreach(var c in GetComponentsInChildren<Collider>()) if(c.enabled && !c.isTrigger && c!=controller) return false;
        incoming=measured; Suspended=true; return true;
    }
    public override bool TryRelease(Vector3 ownerPosition, Transform ownerRoot, Vector3 handVelocity, float age, out Vector3 position, out Vector3 releaseVelocity)
    {
        position=transform.position; releaseVelocity=incoming*Mathf.Exp(-2*Mathf.Max(0,age))+handVelocity;
        if(controller==null) return true;
        Vector3 away=Vector3.ProjectOnPlane(position-ownerPosition,Vector3.up).normalized;
        if(away.sqrMagnitude<.5f) away=Vector3.forward;
        // Reject starting world penetration; Titan contact is expected and excluded
        // for the outward EXIT sweep only, never for candidate overlap clearance.
        if(!Clear(transform.position,ownerRoot)) return false;
        for(int lift=0;lift<3;lift++) for(int step=0;step<7;step++)
        {
            Vector3 candidate=transform.position+away*(step*.8f)+Vector3.up*(lift*.8f);
            if(!Clear(candidate,null)) continue;
            Vector3 delta=candidate-transform.position;
            Vector3 a,b; float radius; Capsule(transform.position,out a,out b,out radius);
            int n=delta.sqrMagnitude>.00001f ? gameObject.scene.GetPhysicsScene().CapsuleCast(a,b,radius,delta.normalized,hits,delta.magnitude,~0,QueryTriggerInteraction.Ignore):0;
            if(n==hits.Length) continue;
            bool blocked=false;
            for(int i=0;i<n;i++) if(!hits[i].transform.IsChildOf(transform) && (ownerRoot==null || !hits[i].transform.IsChildOf(ownerRoot))) blocked=true;
            if(blocked) continue;
            position=candidate; return true;
        }
        return false;
    }
    bool Clear(Vector3 position, Transform ignoredOwner)
    {
        Vector3 a,b;float radius;Capsule(position,out a,out b,out radius);
        int n=gameObject.scene.GetPhysicsScene().OverlapCapsule(a,b,radius,overlaps,~0,QueryTriggerInteraction.Ignore);
        if(n==overlaps.Length) return false;
        for(int i=0;i<n;i++) if(!overlaps[i].transform.IsChildOf(transform) &&
            (ignoredOwner==null || !overlaps[i].transform.IsChildOf(ignoredOwner))) return false;
        return true;
    }
    void Capsule(Vector3 position,out Vector3 a,out Vector3 b,out float radius)
    {
        Vector3 scale=transform.lossyScale;
        radius=controller.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
        float half=Mathf.Max(0,controller.height*Mathf.Abs(scale.y)*.5f-radius);
        Vector3 center=position+transform.TransformVector(controller.center);
        a=center+transform.up*half; b=center-transform.up*half;
    }
    static bool Finite(Vector3 v) { return !(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z)); }
    public override void Resume(Vector3 speed) { Suspended=false; boostedPass=false; IsFlying=true; velocity=measured=speed; }
}
