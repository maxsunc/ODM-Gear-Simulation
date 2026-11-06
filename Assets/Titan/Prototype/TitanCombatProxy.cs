using UnityEngine;

/// <summary>Routes physical, independent temporary ragdoll shapes to the ONE real
/// target/health owner. Never creates duplicate health or increases attack reach.</summary>
public sealed class TitanCombatProxy : MonoBehaviour
{
    public TitanCombatTarget target;
    public static TitanCombatTarget Resolve(Collider shape)
    {
        var direct=shape.GetComponentInParent<TitanCombatTarget>();if(direct!=null)return direct;
        var proxy=shape.GetComponentInParent<TitanCombatProxy>();return proxy!=null?proxy.target:null;
    }
}
