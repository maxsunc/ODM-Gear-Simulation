using System;
using UnityEngine;

/// <summary>Sandbox health and exclusive hold lease, independent of legacy PlayerHealth.
/// Never parent the victim into an animated/detachable skeleton. Real players need an adapter.</summary>
[DefaultExecutionOrder(70)]
[DisallowMultipleComponent]
public class TitanCombatTarget : MonoBehaviour
{
    [Min(1)] public float maxHealth = 100;
    [Range(.5f, 10)] public float maximumHoldDuration = 4;
    public float Health { get; private set; }
    public int HitCount { get; private set; }
    public Vector3 LastImpact { get; private set; }
    public bool IsDead { get { return Health <= 0; } }
    public bool IsHeld { get; private set; }
    public MonoBehaviour HoldOwner { get { return holdOwner; } }
    public string LastReleaseReason { get; private set; }
    public event Action<float> Damaged;
    public event Action HoldReleased;

    MonoBehaviour holdOwner;
    Transform grip;
    Vector3 releasePosition, centerOffset;
    Quaternion releaseRotation;
    float holdAge, leaseDuration;
    Collider[] colliders;
    bool[] colliderStates;
    BodyState[] bodyStates;
    struct BodyState
    {
        public Rigidbody body;
        public bool kinematic, gravity, collisions;
        public CollisionDetectionMode detection;
    }

    void Awake() { ResetHealth(); }
    void LateUpdate() { TickHold(Time.deltaTime); }
    void OnDisable() { ReleaseInternal("Target disabled"); }
    void OnDestroy() { ReleaseInternal("Target destroyed"); }
    public bool IsHeldBy(MonoBehaviour owner) { return IsHeld && owner != null && holdOwner == owner; }
    public void ResetHealth()
    {
        ReleaseInternal("Reset");
        Health = Mathf.Max(1, maxHealth); HitCount = 0;
    }
    public void TakeDamage(float damage, Vector3 impact)
    {
        if (IsDead || !isActiveAndEnabled || damage <= 0 || !Finite(damage)) return;
        Health = Mathf.Max(0, Health - damage); LastImpact = impact; HitCount++;
        if (IsDead) ReleaseInternal("Target died");
        if (Damaged != null) Damaged(damage);
    }

    public bool TryHold(MonoBehaviour owner, Transform socket)
    {
        if (IsHeld || IsDead || !isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled ||
            socket == null || !socket.gameObject.activeInHierarchy || !socket.IsChildOf(owner.transform) ||
            transform.IsChildOf(owner.transform) || owner.transform.IsChildOf(transform) ||
            owner.gameObject.scene != gameObject.scene || socket.gameObject.scene != gameObject.scene ||
            !Finite(socket.position) || !Finite(transform.position)) return false;
        Physics.SyncTransforms();
        releasePosition = transform.position; releaseRotation = transform.rotation;
        colliders = GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[colliders.Length];
        Bounds bounds = new Bounds(transform.position + Vector3.up, Vector3.zero);
        bool found = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderStates[i] = colliders[i].enabled;
            if (!colliders[i].enabled || colliders[i].isTrigger || !colliders[i].gameObject.activeInHierarchy) continue;
            if (!found) { bounds = colliders[i].bounds; found = true; } else bounds.Encapsulate(colliders[i].bounds);
        }
        centerOffset = transform.position - bounds.center;
        var bodies = GetComponentsInChildren<Rigidbody>(true);
        bodyStates = new BodyState[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            var rb = bodies[i];
            bodyStates[i] = new BodyState { body = rb, kinematic = rb.isKinematic, gravity = rb.useGravity,
                collisions = rb.detectCollisions, detection = rb.collisionDetectionMode };
            if (!rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.isKinematic = true; rb.useGravity = false; rb.detectCollisions = false;
        }
        foreach (var col in colliders) col.enabled = false;
        holdOwner = owner; grip = socket; holdAge = 0;
        leaseDuration = Finite(maximumHoldDuration) ? Mathf.Clamp(maximumHoldDuration, .5f, 10) : 4;
        IsHeld = true;
        FollowGrip();
        return true;
    }

    public void TickHold(float deltaTime)
    {
        if (!IsHeld || !Finite(deltaTime) || deltaTime < 0) return;
        if (holdOwner == null || !holdOwner.isActiveAndEnabled || grip == null || !grip.gameObject.activeInHierarchy ||
            !grip.IsChildOf(holdOwner.transform) || holdOwner.gameObject.scene != gameObject.scene ||
            IsDead || !Finite(grip.position)) { ReleaseInternal("Hold invalidated"); return; }
        holdAge += deltaTime;
        if (holdAge >= leaseDuration) { ReleaseInternal("Hold lease expired"); return; }
        FollowGrip();
    }
    void FollowGrip() { transform.position = grip.position + centerOffset; }
    public bool ReleaseHold(MonoBehaviour owner, string reason = "Released")
    {
        if (!IsHeldBy(owner)) return false;
        ReleaseInternal(reason); return true;
    }
    public void EscapeHold() { ReleaseInternal("Escaped"); }
    void ReleaseInternal(string reason)
    {
        if (!IsHeld) return;
        // Clear ownership before callbacks: release/cancel can re-enter each other.
        IsHeld = false; holdOwner = null; grip = null; LastReleaseReason = reason;
        // Deliberate sandbox policy: return to the captured pre-grab world pose,
        // not an airborne drop or a parent that might disappear with a severed limb.
        transform.SetPositionAndRotation(releasePosition, releaseRotation);
        foreach (var state in bodyStates)
        {
            var rb = state.body; if (rb == null) continue;
            rb.position = rb.transform.position; rb.rotation = rb.transform.rotation;
            rb.isKinematic = state.kinematic; rb.useGravity = state.gravity;
            rb.collisionDetectionMode = state.detection; rb.detectCollisions = state.collisions;
            if (!rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
        if (HoldReleased != null) HoldReleased();
    }
    /// <summary>CharacterController's native query capsule is shrunken and does not
    /// support Collider.ClosestPoint consistently. Use its configured capsule for
    /// combat geometry, without enlarging the attack's radius.</summary>
    public static Vector3 ClosestCombatPoint(Collider collider, Vector3 point)
    {
        var controller = collider as CharacterController;
        if (controller == null) return collider.ClosestPoint(point);
        Vector3 scale = controller.transform.lossyScale;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float segment = Mathf.Max(0, controller.height * Mathf.Abs(scale.y) * .5f - radius);
        Vector3 center = controller.transform.TransformPoint(controller.center), axis = controller.transform.up;
        Vector3 nearest = center + axis * Mathf.Clamp(Vector3.Dot(point - center, axis), -segment, segment);
        Vector3 delta = point - nearest;
        return delta.sqrMagnitude <= radius * radius ? point : nearest + delta.normalized * radius;
    }
    static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
}
