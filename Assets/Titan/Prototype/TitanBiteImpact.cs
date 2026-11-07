using UnityEngine;

/// <summary>Confirmed held bite context. Presentation captures BEFORE safe release;
/// completion only presents blood if real HP was lost. No second health authority.</summary>
public struct TitanBiteImpact
{
    public Transform Attacker;
    public Vector3 Point, Direction, Velocity;
    public float Damage;
    public bool Lethal;
}
