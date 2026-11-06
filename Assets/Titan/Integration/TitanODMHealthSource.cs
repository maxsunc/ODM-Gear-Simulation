using UnityEngine;

/// <summary>Uses the real player's HP and death/respawn, not the proxy HP pool.</summary>
[RequireComponent(typeof(PlayerHealth))]
public class TitanODMHealthSource : TitanHealthSource
{
    PlayerHealth player;
    bool eatenSoundPlayed;
    public int EatenSoundCount {get;private set;}
    void Awake() { player=GetComponent<PlayerHealth>(); }
    void OnEnable() { if(player==null) player=GetComponent<PlayerHealth>();player.Died+=OnDeath; }
    void OnDisable() { if(player!=null) player.Died-=OnDeath; }
    void OnDeath() { var target=GetComponent<TitanCombatTarget>();if(target!=null) target.EscapeHold(); }
    public override bool UsesMouthInsertion { get { return player != null && player.EnsureScoutRagdoll() != null; } }
    public override void PrepareStomp(Transform attacker,Vector3 point,float damage)
    {if(player!=null)player.PrepareStomp(attacker,point,damage);}
    public override void StompApplied(Transform attacker,Vector3 point)
    {if(player!=null && !player.isDead)player.ReactToStomp(attacker,point);}
    public override void PrepareHeldDeath(Transform attacker,Vector3 velocity)
    {
        var presentation=player!=null?player.EnsureScoutRagdoll():null;
        if(presentation!=null)presentation.PrepareHeldDeath(attacker,velocity);
    }
    public override void PrepareBite(TitanBiteImpact impact)
    {
        if(player!=null && !player.isDead)eatenSoundPlayed=false;
        var presentation = player != null ? player.EnsureScoutRagdoll() : null;
        if (presentation != null) presentation.PrepareBite(impact);
    }
    public override void CompleteBite(TitanBiteImpact impact, bool damaged)
    {
        var presentation = player != null ? player.GetComponent<ScoutPlayerRagdoll>() : null;
        if (presentation != null) presentation.CompleteBite();
        var feedback = GetComponent<TitanSwordFeedback>();
        if (damaged && feedback != null) feedback.PlayBiteBlood(impact.Point, impact.Direction);
        if(damaged && impact.Lethal && player!=null && player.isDead && !eatenSoundPlayed && player.GetComponent<PlayerDeathFeedback>()!=null && player.GetComponent<PlayerDeathFeedback>().BloodSoundCount>0)
        {eatenSoundPlayed=true;EatenSoundCount++;}
    }
    public override bool IsBodyCollider(Collider shape) { return shape==GetComponent<CharacterController>(); }
    public override float CurrentHealth { get { return isActiveAndEnabled && player!=null && !player.isDead?player.currentHP:0; } }
    public override void ApplyDamage(float damage) { if(player!=null) player.TakeDamage(damage); }
}
