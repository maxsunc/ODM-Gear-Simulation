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
    float sandboxHealth;
    TitanHealthSource healthSource;
    public float Health { get { return healthSource!=null?healthSource.CurrentHealth:sandboxHealth; } private set { sandboxHealth=value; } }
    public int HitCount { get; private set; }
    public Vector3 LastImpact { get; private set; }
    public bool IsDead { get { return Health <= 0; } }
    public bool IsHeld { get; private set; }
    public bool ReleasePending { get; private set; }
    TitanAerialMotor aerialMotor;
    bool aerialHold;
    Vector3 previousHeldPosition, handVelocity, lastOwnerPosition;
    Transform releaseOwner, targetRoot;
    Vector3 heldCenterOffset;
    Quaternion carryRotation=Quaternion.identity;
    public Vector3 HeldCenterOffset(MonoBehaviour owner) { return IsHeldBy(owner)?carryRotation*heldCenterOffset:Vector3.zero; }
    // Rotate around the existing grip, never recenter/reparent the human. Upright
    // aerial controllers support yaw; ground mode retains its original follow.
    public bool SetAerialCarryYaw(MonoBehaviour owner,float degrees)
    {
        if(!IsHeldBy(owner) || !aerialHold || !Finite(degrees) || Mathf.Abs(degrees)>360) return false;
        carryRotation=Quaternion.AngleAxis(degrees,Vector3.up);return true;
    }
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

    void Awake() { targetRoot=transform; healthSource=GetComponent<TitanHealthSource>(); ResetHealth(); }
    void LateUpdate() { TickHold(Time.deltaTime); }
    void OnDisable() { ReleaseInternal("Target disabled"); }
    void OnDestroy()
    {
        ReleaseInternal("Target destroyed");
        // A removed health component must not strand its surviving controller.
        if(ReleasePending && aerialMotor!=null) aerialMotor.ContinueRelease(() =>
        { if(targetRoot==null) return true; TryCompleteAerialRelease(); return !ReleasePending; });
    }
    public bool IsHeldBy(MonoBehaviour owner) { return IsHeld && owner != null && holdOwner == owner; }
    // Explicit attack fast-forward must not bypass a lease whose LateUpdate has
    // not run yet. The logical age is a minimum, not a second ticking clock.
    public bool IsHoldWithinLease(MonoBehaviour owner, float minimumAge)
    {
        return IsHeldBy(owner) && Finite(minimumAge) && minimumAge >= 0 && Mathf.Max(holdAge,minimumAge) < leaseDuration;
    }
    public void ResetHealth()
    {
        ReleaseInternal("Reset");
        Health = Mathf.Max(1, maxHealth); HitCount = 0;
    }
    public void TakeDamage(float damage, Vector3 impact)
    {
        if (IsDead || !isActiveAndEnabled || damage <= 0 || !Finite(damage)) return;
        // Restore the held controller BEFORE external lethal callbacks enable a
        // player ragdoll. Never restore cached animation flags over a new death.
        if(healthSource!=null)
        {
            if(damage>=Health) ReleaseInternal("Target died");
            healthSource.ApplyDamage(damage);
        }
        else Health = Mathf.Max(0, Health - damage);
        LastImpact = impact; HitCount++;
        if (IsDead) ReleaseInternal("Target died");
        if (Damaged != null) Damaged(damage);
    }

    public bool TryHold(MonoBehaviour owner, Transform socket, bool airborne = false, Vector3? contactPosition = null, Quaternion? contactRotation = null)
    {
        if (ReleasePending || IsHeld || IsDead || !isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled ||
            socket == null || !socket.gameObject.activeInHierarchy || !socket.IsChildOf(owner.transform) ||
            transform.IsChildOf(owner.transform) || owner.transform.IsChildOf(transform) ||
            owner.gameObject.scene != gameObject.scene || socket.gameObject.scene != gameObject.scene ||
            !Finite(socket.position) || !Finite(transform.position) || (contactPosition.HasValue && !Finite(contactPosition.Value)) ||
            (contactRotation.HasValue && !Finite(contactRotation.Value))) return false;
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
        aerialMotor=GetComponent<TitanAerialMotor>();
        bool motorHold=airborne || (aerialMotor!=null && aerialMotor.SuspendGroundHolds);
        if(motorHold && (aerialMotor==null || !aerialMotor.CanSuspend || !aerialMotor.TrySuspend())) return false;
        aerialHold=motorHold; // Real ground holds use safe current-pose release too.
        Vector3 capturePosition=contactPosition ?? transform.position;
        Quaternion captureRotation=contactRotation ?? transform.rotation;
        Vector3 shapeCenter=bounds.center-transform.position;
        var capsule=GetComponent<CharacterController>();
        if(airborne && capsule!=null) shapeCenter=captureRotation*Vector3.Scale(capsule.center,transform.lossyScale);
        centerOffset = airborne ? capturePosition-socket.position : transform.position-bounds.center;
        heldCenterOffset=shapeCenter+centerOffset;
        if(airborne) releaseRotation=captureRotation;
        carryRotation=Quaternion.identity;
        previousHeldPosition=capturePosition; handVelocity=Vector3.zero; lastOwnerPosition=owner.transform.position;
        releaseOwner=owner.transform;
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
        if(airborne) transform.SetPositionAndRotation(capturePosition,captureRotation); // physical CCD time, not detection aim
        holdOwner = owner; grip = socket; holdAge = 0;
        leaseDuration = Finite(maximumHoldDuration) ? Mathf.Clamp(maximumHoldDuration, .5f, 10) : 4;
        IsHeld = true;
        FollowGrip();
        return true;
    }

    public void TickHold(float deltaTime)
    {
        if (!Finite(deltaTime) || deltaTime < 0) return;
        if(ReleasePending) TryCompleteAerialRelease();
        if(!IsHeld) return;
        if (holdOwner == null || !holdOwner.isActiveAndEnabled || grip == null || !grip.gameObject.activeInHierarchy ||
            !grip.IsChildOf(holdOwner.transform) || holdOwner.gameObject.scene != gameObject.scene ||
            IsDead || !Finite(grip.position)) { ReleaseInternal("Hold invalidated"); return; }
        Vector3 desired=grip.position+(aerialHold?carryRotation*centerOffset:centerOffset);
        // Include the held person's orbital velocity when the wrist turns them,
        // not just socket translation; release must not discard that momentum.
        if(deltaTime>0) handVelocity=(desired-previousHeldPosition)/deltaTime;
        previousHeldPosition=desired; lastOwnerPosition=holdOwner.transform.position;
        holdAge += deltaTime;
        if (holdAge >= leaseDuration) { ReleaseInternal("Hold lease expired"); return; }
        FollowGrip();
    }
    void FollowGrip()
    {
        transform.position=grip.position+(aerialHold?carryRotation*centerOffset:centerOffset);
        if(aerialHold) transform.rotation=carryRotation*releaseRotation;
    }
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
        if(aerialHold)
        {
            ReleasePending=true;
            TryCompleteAerialRelease();
        }
        else RestorePhysics(releasePosition,releaseRotation,Vector3.zero);
        if (HoldReleased != null) HoldReleased();
    }
    void TryCompleteAerialRelease()
    {
        Vector3 position,velocity;
        if(aerialMotor==null || !aerialMotor.TryRelease(lastOwnerPosition,releaseOwner,handVelocity,holdAge,out position,out velocity)) return;
        if(!Finite(position) || !Finite(velocity)) return;
        ReleasePending=false;
        RestorePhysics(position,targetRoot.rotation,velocity);
        aerialMotor.Resume(velocity);
    }
    void RestorePhysics(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        // Ground retains the approved snap-back policy. Air NEVER uses it.
        targetRoot.SetPositionAndRotation(position, rotation);
        foreach (var state in bodyStates)
        {
            var rb = state.body; if (rb == null) continue;
            rb.position = rb.transform.position; rb.rotation = rb.transform.rotation;
            rb.isKinematic = state.kinematic; rb.useGravity = state.gravity;
            rb.collisionDetectionMode = state.detection; rb.detectCollisions = state.collisions;
            if (!rb.isKinematic) { rb.velocity = velocity; rb.angularVelocity = Vector3.zero; }
        }
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
    }
    /// <summary>Actual supported shape contact while hold physics is disabled. Never
    /// temporarily enable a held collider or use its empty disabled bounds.</summary>
    public bool TryGetHeldContact(MonoBehaviour owner, Vector3 point, float radius, out Vector3 contact)
    {
        contact = point;
        if (!IsHeldBy(owner) || !Finite(point) || !Finite(radius) || radius < 0) return false;
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null || !colliderStates[i] || col.isTrigger || !col.gameObject.activeInHierarchy ||
                !col.transform.IsChildOf(transform) || (healthSource!=null && !healthSource.IsBodyCollider(col))) continue;
            // Unknown disabled mesh shapes fail closed; a real controller needs an adapter.
            if (!(col is BoxCollider || col is SphereCollider || col is CapsuleCollider || col is CharacterController)) continue;
            Vector3 closest = ClosestCombatPoint(col, point);
            if ((closest - point).sqrMagnitude > radius * radius) continue;
            contact = closest; return true;
        }
        return false;
    }

    /// <summary>CharacterController's native query capsule is shrunken and does not
    /// support Collider.ClosestPoint consistently. Use its configured capsule for
    /// combat geometry, without enlarging the attack's radius.</summary>
    public static Vector3 ClosestCombatPoint(Collider collider, Vector3 point)
    {
        var box = collider as BoxCollider;
        if (box != null)
        {
            Vector3 local = box.transform.InverseTransformPoint(point) - box.center, half = box.size * .5f;
            return box.transform.TransformPoint(box.center + new Vector3(Mathf.Clamp(local.x,-half.x,half.x),
                Mathf.Clamp(local.y,-half.y,half.y), Mathf.Clamp(local.z,-half.z,half.z)));
        }
        var sphere = collider as SphereCollider;
        if (sphere != null)
        {
            Vector3 s = sphere.transform.lossyScale, c = sphere.transform.TransformPoint(sphere.center), d = point-c;
            float r = sphere.radius * Mathf.Max(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z));
            return d.sqrMagnitude <= r*r ? point : c + d.normalized*r;
        }
        var capsule = collider as CapsuleCollider;
        if (capsule != null)
        {
            Vector3 s = capsule.transform.lossyScale;
            int axisIndex = capsule.direction;
            float r = capsule.radius * Mathf.Max(Mathf.Abs(s[(axisIndex+1)%3]),Mathf.Abs(s[(axisIndex+2)%3]));
            Vector3 localAxis = Vector3.zero; localAxis[axisIndex]=1;
            Vector3 axisWorld = capsule.transform.TransformDirection(localAxis).normalized;
            float half = Mathf.Max(0,capsule.height*Mathf.Abs(s[axisIndex])*.5f-r);
            Vector3 c = capsule.transform.TransformPoint(capsule.center);
            Vector3 near = c + axisWorld*Mathf.Clamp(Vector3.Dot(point-c,axisWorld),-half,half), d = point-near;
            return d.sqrMagnitude <= r*r ? point : near+d.normalized*r;
        }
        var controller = collider as CharacterController;
        if (controller == null) return collider.ClosestPoint(point);
        Vector3 scale = controller.transform.lossyScale;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float segment = Mathf.Max(0, controller.height * Mathf.Abs(scale.y) * .5f - radius);
        Vector3 center = controller.transform.TransformPoint(controller.center), axis = Vector3.up;
        Vector3 nearest = center + axis * Mathf.Clamp(Vector3.Dot(point - center, axis), -segment, segment);
        Vector3 delta = point - nearest;
        return delta.sqrMagnitude <= radius * radius ? point : nearest + delta.normalized * radius;
    }
    static bool Finite(Quaternion q) { float length=Quaternion.Dot(q,q);return Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w)&&Mathf.Abs(length-1)<.01f; }
    static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
}
