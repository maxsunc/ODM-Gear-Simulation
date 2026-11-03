using UnityEngine;

/// <summary>Optional external health bridge. Labs keep their own health; gameplay
/// supplies its real health/death source without a second competing HP pool.</summary>
public abstract class TitanHealthSource : MonoBehaviour
{
    public virtual bool IsBodyCollider(Collider shape) { return true; }
    public abstract float CurrentHealth { get; }
    public abstract void ApplyDamage(float damage);
}
