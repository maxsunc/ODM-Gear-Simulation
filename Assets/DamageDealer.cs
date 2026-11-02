using System.Collections.Generic;
using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [Header("Damage Settings")]
    public float baseDamage = 10f;
    public float momentumMultiplier = 2.5f;

    private ODM_Movement odmMovement;
    private readonly HashSet<long> hitRegionsThisSwing = new HashSet<long>();

    void OnEnable()
    {
        hitRegionsThisSwing.Clear();
    }

    void Start()
    {
        // Find ODM_Movement on the player (parent hierarchy)
        odmMovement = GetComponentInParent<ODM_Movement>();
        if (odmMovement == null)
        {
            odmMovement = FindObjectOfType<ODM_Movement>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        float playerSpeed = 0f;
        if (odmMovement != null)
        {
            playerSpeed = odmMovement.CurrentSpeed + odmMovement.ResidualSpeed;
        }

        float totalDamage = baseDamage + (playerSpeed * momentumMultiplier);

        // Modular prototype zones share health across all colliders on an arm/leg.
        // Re-enabling the existing sword attack object begins a new swing.
        TitanHitZone zone = other.GetComponent<TitanHitZone>();
        if (zone != null && zone.owner != null)
        {
            long key = ((long)zone.owner.GetInstanceID() << 32) | (uint)zone.region;
            if (!hitRegionsThisSwing.Add(key)) return;
            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 direction = zone.owner.transform.position - transform.position;
            zone.Hit(totalDamage, point, direction.normalized * Mathf.Min(totalDamage, 150f));
            return;
        }

        // Check for titan limb
        TitanLimb limb = other.GetComponent<TitanLimb>();
        if (limb != null)
        {
            limb.TakeDamage(totalDamage);
            return;
        }

        // Check for titan nape
        TitanNape nape = other.GetComponent<TitanNape>();
        if (nape != null)
        {
            nape.TakeDamage(totalDamage);
            return;
        }
    }
}
