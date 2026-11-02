using UnityEngine;

/// <summary>One of several trigger volumes sharing damage with a single body region.</summary>
[DisallowMultipleComponent]
public class TitanHitZone : MonoBehaviour
{
    public ModularTitanBody owner;
    public TitanHitRegion region;

    public void Hit(float damage, Vector3 point, Vector3 impulse)
    {
        if (owner == null) owner = GetComponentInParent<ModularTitanBody>();
        if (owner != null) owner.ApplyDamage(region, damage, point, impulse);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = region == TitanHitRegion.Nape ? Color.red :
            region == TitanHitRegion.Eyes ? Color.yellow : new Color(0, 0.8f, 1, 0.5f);
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
