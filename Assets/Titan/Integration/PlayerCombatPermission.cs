using UnityEngine;

/// <summary>Shared fail-closed gate for input, animation and every damage path.
/// Zero movement-derived damage is not sufficient: base damage must also stop.</summary>
public static class PlayerCombatPermission
{
    public static bool IsBlocked(Component source)
    {
        if(source==null)return true;
        var health=source.GetComponentInParent<PlayerHealth>();
        if(health!=null && (health.isDead || health.isGrabbed))return true;
        var target=source.GetComponentInParent<TitanCombatTarget>();
        if(target!=null && (target.IsHeld || target.ReleasePending))return true;
        var walking=source.GetComponentInParent<Movement>();
        if(walking!=null && walking.TitanSuspended)return true;
        var gear=source.GetComponentInParent<ODM_Movement>();
        return gear!=null && gear.TitanSuspended;
    }
}
