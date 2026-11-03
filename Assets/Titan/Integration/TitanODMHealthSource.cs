using UnityEngine;

/// <summary>Uses the real player's HP and death/respawn, not the proxy HP pool.</summary>
[RequireComponent(typeof(PlayerHealth))]
public class TitanODMHealthSource : TitanHealthSource
{
    PlayerHealth player;
    void Awake() { player=GetComponent<PlayerHealth>(); }
    void OnEnable() { if(player==null) player=GetComponent<PlayerHealth>();player.Died+=OnDeath; }
    void OnDisable() { if(player!=null) player.Died-=OnDeath; }
    void OnDeath() { var target=GetComponent<TitanCombatTarget>();if(target!=null) target.EscapeHold(); }
    public override bool IsBodyCollider(Collider shape) { return shape==GetComponent<CharacterController>(); }
    public override float CurrentHealth { get { return isActiveAndEnabled && player!=null && !player.isDead?player.currentHP:0; } }
    public override void ApplyDamage(float damage) { if(player!=null) player.TakeDamage(damage); }
}
