using UnityEngine;

/// <summary>Opt-in controller contract. Aerial grabs refuse targets without one.
/// Implementations own rope/boost suspension, release clearance and momentum.
/// Returning false from TryRelease keeps physics suspended, independently of the Titan.</summary>
public abstract class TitanAerialMotor : MonoBehaviour
{
    System.Func<bool> releasePoll;
    public void ContinueRelease(System.Func<bool> poll) { releasePoll=poll; }
    // New-life owner clears dead release continuations before restoring live controls.
    public void ClearReleaseContinuation() { releasePoll=null; }
    void LateUpdate() { if(releasePoll!=null && releasePoll()) releasePoll=null; }
    void OnDisable() { var target=GetComponent<TitanCombatTarget>(); if(target!=null) target.EscapeHold(); }
    // Production ODM can tilt its root during flight. Native Unity CCT axis stays
    // world-up, but its offset center follows root rotation. Opt in to that CCD.
    public virtual bool SupportsRotatingCapsule { get { return false; } }
    public virtual bool SuspendGroundHolds { get { return false; } }
    public abstract Vector3 Velocity { get; }
    public abstract bool CanSuspend { get; }
    public abstract bool TrySuspend();
    public abstract bool TryRelease(Vector3 ownerPosition, Transform ownerRoot, Vector3 handVelocity, float age, out Vector3 position, out Vector3 velocity);
    public abstract void Resume(Vector3 velocity);
}
