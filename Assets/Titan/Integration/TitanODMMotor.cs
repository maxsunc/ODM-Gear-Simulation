using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene opt-in bridge, outside the prototype asmdef to reference the real
/// legacy controller. Does not change free-flight tuning. Catch disconnects hooks
/// and cancels jump/boost/inertia coroutines; release requires whole-gear clearance.</summary>
[DefaultExecutionOrder(30)]
[RequireComponent(typeof(Movement),typeof(ODM_Movement),typeof(CharacterController))]
public class TitanODMMotor : TitanAerialMotor
{
    Movement walking;
    ODM_Movement gear;
    CharacterController controller;
    PlayerHealth player;
    TitanCombatTarget target;
    TitanReleaseClearance clearance;
    Vector3 measured,incoming,lastPosition;
    bool walkingEnabled,gearEnabled;
    readonly List<Behaviour> stoppedRotators=new List<Behaviour>();
    public bool Suspended { get; private set; }
    public override bool SupportsRotatingCapsule { get { return true; } }
    public override bool SuspendGroundHolds { get { return true; } }
    public override Vector3 Velocity { get { return measured; } }
    public override bool CanSuspend { get { return isActiveAndEnabled && !Suspended && walking!=null && gear!=null &&
        !walking.TitanSuspended && !gear.TitanSuspended && controller!=null && controller.enabled && player!=null && !player.isDead; } }
    void Awake()
    {
        walking=GetComponent<Movement>();gear=GetComponent<ODM_Movement>();controller=GetComponent<CharacterController>();
        player=GetComponent<PlayerHealth>();target=GetComponent<TitanCombatTarget>();lastPosition=transform.position;
    }
    void Update()
    {
        if(target==null) target=GetComponent<TitanCombatTarget>();
        if(player!=null) { player.isGrabbed=target!=null && (target.IsHeld || target.ReleasePending);
            player.grabbedByTitan=target!=null && target.HoldOwner!=null?target.HoldOwner.transform:null; }
    }
    // Legacy movement writes in FixedUpdate. Sampling after both real motors
    // avoids 0 / inflated velocity alternation on high-refresh render frames.
    void FixedUpdate() { SampleMotion(Time.fixedDeltaTime); }
    public void SampleMotion(float dt)
    {
        if(!Suspended && dt>0 && !float.IsNaN(dt) && !float.IsInfinity(dt)) measured=(transform.position-lastPosition)/dt;
        lastPosition=transform.position;
    }
    public void ResetMotionHistory() { lastPosition=transform.position;measured=Vector3.zero; }
    public override bool TrySuspend()
    {
        if(!CanSuspend) return false;
        var solids=new List<Collider>();
        foreach(var c in GetComponentsInChildren<Collider>()) if(c.enabled && !c.isTrigger)
        { if(!TitanReleaseClearance.Supported(c)) return false;solids.Add(c); }
        if(!solids.Contains(controller)) return false;
        incoming=measured;walkingEnabled=walking.enabled;gearEnabled=gear.enabled;
        if(!walking.SuspendForTitan(this)) return false;
        if(!gear.SuspendForTitan(this)) { walking.ResumeAfterTitan(this,Vector3.zero);return false; }
        clearance=new TitanReleaseClearance(transform,solids.ToArray());
        stoppedRotators.Clear();
        foreach(var c in FindObjectsOfType<ThirdPersonCam>())
            if(c.enabled && c.movementRotator!=null && c.movementRotator.IsChildOf(transform))
            { stoppedRotators.Add(c);c.enabled=false; }
        Suspended=true;player.isGrabbed=true;
        var attack=GetComponent<Attack>();if(attack!=null)attack.CancelAttack();
        return true;
    }
    public override bool TryRelease(Vector3 ownerPosition,Transform ownerRoot,Vector3 carryVelocity,float age,out Vector3 position,out Vector3 velocity)
    {
        velocity=incoming*Mathf.Exp(-2*Mathf.Max(0,age))+carryVelocity;
        position=transform.position;
        return clearance!=null && clearance.TryFindExit(ownerPosition,ownerRoot,out position);
    }
    public override void Resume(Vector3 velocity)
    {
        if(!Suspended) return;
        Suspended=false;lastPosition=transform.position;measured=velocity;
        player.isGrabbed=false;player.grabbedByTitan=null;
        if(!player.isDead)
        {
            walking.ResumeAfterTitan(this,velocity);gear.ResumeAfterTitan(this,velocity);
            walking.enabled=walkingEnabled;gear.enabled=gearEnabled;
            foreach(var c in stoppedRotators) if(c!=null) c.enabled=true;
        }
        stoppedRotators.Clear();
    }
}
