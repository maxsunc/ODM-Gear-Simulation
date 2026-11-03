using UnityEngine;

/// <summary>Opt-in controller contract. Aerial grabs refuse targets without one.
/// Implementations own rope/boost suspension, release clearance and momentum.
/// Returning false from TryRelease keeps physics suspended, independently of the Titan.</summary>
public abstract class TitanAerialMotor : MonoBehaviour
{
    System.Func<bool> releasePoll;
    public void ContinueRelease(System.Func<bool> poll) { releasePoll=poll; }
    void LateUpdate() { if(releasePoll!=null && releasePoll()) releasePoll=null; }
    void OnDisable() { var target=GetComponent<TitanCombatTarget>(); if(target!=null) target.EscapeHold(); }
    public abstract Vector3 Velocity { get; }
    public abstract bool CanSuspend { get; }
    public abstract bool TrySuspend();
    public abstract bool TryRelease(Vector3 ownerPosition, Transform ownerRoot, Vector3 handVelocity, float age, out Vector3 position, out Vector3 velocity);
    public abstract void Resume(Vector3 velocity);
}
