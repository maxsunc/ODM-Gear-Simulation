using UnityEngine;

/// <summary>Optional external health bridge. Labs keep their own health; gameplay
/// supplies its real health/death source without a second competing HP pool.</summary>
public abstract class TitanHealthSource : MonoBehaviour
{
    public virtual bool IsBodyCollider(Collider shape) { return true; }
    public virtual bool UsesMouthInsertion { get { return false; } }
    public virtual void PrepareBite(TitanBiteImpact impact) { }
    public virtual void CompleteBite(TitanBiteImpact impact, bool damaged) { }
    public abstract float CurrentHealth { get; }
    public abstract void ApplyDamage(float damage);
}
