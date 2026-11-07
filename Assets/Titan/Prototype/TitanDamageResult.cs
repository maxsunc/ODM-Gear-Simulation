using UnityEngine;

public enum TitanDamageOutcome { None, Hit, Severed, Blinded, Killed }

/// <summary>A receipt for one accepted region hit. Damage includes overkill; AppliedDamage does not.</summary>
public struct TitanDamageResult
{
    public ModularTitanBody Body;
    public TitanHitRegion Region;
    public TitanDamageOutcome Outcome;
    public float Damage, AppliedDamage, RemainingHealth;
    public Vector3 Point, Direction;
    public bool Accepted { get { return Outcome != TitanDamageOutcome.None; } }
}
