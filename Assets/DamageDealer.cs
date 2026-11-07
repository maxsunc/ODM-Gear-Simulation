using System;
using System.Collections.Generic;
using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [Header("Damage Settings")]
    public float baseDamage = 10f;
    public float momentumMultiplier = 2.5f;
    [Header("Scene opt-in: speed damage")]
    public bool speedScaledDamage;
    [Min(0)] public float speedBonusStartsAt = 15f;
    [Min(0)] public float damagePerSpeed = 35f;
    [Min(1)] public float maximumDamageSpeed = 80f;
    public event Action<TitanDamageResult> HitConfirmed;
    ODM_Movement odmMovement;
    TitanSwordFeedback feedback;
    readonly HashSet<long> hitRegionsThisSwing = new HashSet<long>();

    void OnEnable() { hitRegionsThisSwing.Clear(); }
    void Start() { ResolvePlayer(); }
    void ResolvePlayer()
    {
        if(odmMovement==null)odmMovement=GetComponentInParent<ODM_Movement>();
        if(odmMovement==null && !speedScaledDamage)odmMovement=FindObjectOfType<ODM_Movement>();
        if(feedback==null)feedback=GetComponentInParent<TitanSwordFeedback>();
    }
    public float CalculateDamage(float actualSpeed)
    {
        if(float.IsNaN(actualSpeed)||float.IsInfinity(actualSpeed)||actualSpeed<0)actualSpeed=0;
        return Mathf.Max(0,baseDamage)+Mathf.Max(0,damagePerSpeed)*Mathf.Max(0,Mathf.Min(actualSpeed,Mathf.Max(1,maximumDamageSpeed))-Mathf.Max(0,speedBonusStartsAt));
    }
    // All blades and native contact paths share one per-swing region lease.
    public void ProcessContact(Collider other)
    {
        if(other==null)return;
        Vector3 point=other.ClosestPoint(transform.position);
        ProcessContact(other,point,other.transform.position-transform.position);
    }
    public void ProcessContact(Collider other, Vector3 point, Vector3 slashDirection)
    {
        if(!isActiveAndEnabled||other==null||PlayerCombatPermission.IsBlocked(this))return;
        ResolvePlayer();
        var zone=other.GetComponent<TitanHitZone>();
        float damage=speedScaledDamage ? CalculateDamage(feedback!=null?feedback.DamageSpeedFor(zone!=null?zone.owner:null):0) :
            baseDamage+(odmMovement!=null?(odmMovement.CurrentSpeed+odmMovement.ResidualSpeed)*momentumMultiplier:0);
        if(zone!=null && zone.owner!=null)
        {
            long key=((long)zone.owner.GetInstanceID()<<32)|(uint)zone.region;
            if(!hitRegionsThisSwing.Add(key))return;
            Vector3 direction=slashDirection.sqrMagnitude>.000001f?slashDirection.normalized:zone.owner.transform.forward;
            var result=zone.owner.ReceiveDamage(zone.region,damage,point,direction*Mathf.Min(damage,150));
            if(result.Accepted)
            {
                GameStats.RecordHit(this,result);
                if(HitConfirmed!=null)HitConfirmed(result);
            }
            return;
        }
        var limb=other.GetComponent<TitanLimb>();if(limb!=null){limb.TakeDamage(damage);return;}
        var nape=other.GetComponent<TitanNape>();if(nape!=null)nape.TakeDamage(damage);
    }
    void OnTriggerEnter(Collider other)
    {
        // New scene uses the ACTUAL cutting meshes, not legacy hilt/gear triggers.
        var sweep=GetComponent<TitanODMSwordSweep>();
        if(speedScaledDamage && sweep!=null && sweep.isActiveAndEnabled)return;
        ProcessContact(other);
    }
}
