using UnityEngine;

/// <summary>Original fixed-step eased follow/FOV, with the retained improved
/// zoom and render-time collision safety. Never writes the player or physics state.</summary>
[DefaultExecutionOrder(150)]
public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 2f, 0); // Intentionally world-space.

    [Header("Camera Settings")]
    public float distance = 6f;
    public float minDistance = 2f;
    public float maxDistance = 12f;
    [Tooltip("Metres per wheel notch; independent of the old ScrollWheel axis sensitivity.")]
    [Min(0)] public float zoomUnitsPerNotch = 1.25f;
    [Min(.01f)] public float zoomSmoothTime = .08f;

    [Header("Mouse Input")]
    public float mouseSensitivity = 2f;
    public bool invertY = false;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 60f;

    [Header("Smoothing")]
    public float rotationSmoothing = 5f; // Serialized compatibility; rotation was always direct.
    [Tooltip("Original positional follow easing and collision recovery rate.")]
    public float positionSmoothing = 8f;

    [Header("Collision")]
    [Tooltip("When off, the camera can pass through scenery. Does not affect player or hook collisions.")]
    public bool cameraCollisionEnabled = false;
    public LayerMask collisionLayers = -1;
    public float collisionBuffer = .3f;
    [Tooltip("Minimum camera sphere radius; enlarged if necessary to protect near-plane corners.")]
    [Min(.01f)] public float collisionRadius = .3f;

    [Header("Dynamic FOV")]
    public float baseFOV = 60f;
    public float maxFOV = 90f;
    public float speedThreshold = 10f;
    public float fovSmoothSpeed = 3f;

    float currentX, currentY, currentDistance, smoothedZoomDistance, zoomVelocity;
    Camera cam;
    Vector3 lastTargetPosition;
    float currentSpeed;
    Transform observedTarget, ignoredRoot;
    CharacterController playerController;
    bool recoveringFromCollision, hasSafePosition;
    Vector3 lastSafePosition, pivotAdjustment;
    readonly RaycastHit[] hits = new RaycastHit[64];
    readonly Collider[] overlaps = new Collider[64];

    void Start()
    {
        if(target == null) { Debug.LogError("No target assigned!", this); return; }
        cam = GetComponent<Camera>();
        if(cam == null) { Debug.LogError("No Camera component found!", this); return; }
        currentX = target.eulerAngles.y; currentY = 0;
        ResetAfterTeleport();
    }
    public void ResetAfterTeleport()
    {
        if(target == null) return;
        if(cam == null) cam = GetComponent<Camera>();
        observedTarget = target;
        playerController = target.GetComponentInParent<CharacterController>();
        var health = target.GetComponentInParent<PlayerHealth>();
        ignoredRoot = health != null ? health.transform : playerController != null ? playerController.transform : target;
        lastTargetPosition = target.position; currentSpeed = 0;
        if(cam != null) cam.fieldOfView = Safe(baseFOV, 60, 1, 179);
        smoothedZoomDistance = currentDistance = DesiredDistance(); zoomVelocity = 0;
        recoveringFromCollision = false; hasSafePosition = false; pivotAdjustment = Vector3.zero;
        UpdateCamera(0, true);
    }
    void Update()
    {
        // UI scroll/look while the J menu is open must not alter the orbit.
        if(target == null || Time.timeScale <= 0 || Cursor.lockState != CursorLockMode.Locked) return;
        HandleInput();
    }
    void FixedUpdate()
    {
        if(target == null || cam == null || Time.timeScale <= 0) return;
        if(observedTarget != target) ResetAfterTeleport();
        UpdateSpeed();
        UpdateFOV(); // Final lens before checking near-plane collision clearance.
        UpdateCamera(Time.fixedDeltaTime, false);
    }
    void LateUpdate()
    {
        if(target == null || cam == null || Time.timeScale <= 0 || !cameraCollisionEnabled) return;
        if(observedTarget != target) ResetAfterTeleport();
        // Only safety is rechecked after render-time animation/moving obstacles.
        // A zero step never advances follow, orbit, zoom or FOV a second time.
        UpdateCamera(0, false);
    }
    void HandleInput()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * GameOptions.LookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * GameOptions.LookSensitivity;
        if(GameOptions.ResolveInvertY(invertY)) mouseY = -mouseY;
        currentX += mouseX; currentY = Mathf.Clamp(currentY - mouseY, minVerticalAngle, maxVerticalAngle);
        // Raw wheel delta preserves fractional trackpad input, without .1 axis
        // scaling, a small-input dead zone or a frame-time multiplier.
        float scroll = Input.mouseScrollDelta.y;
        if(Finite(scroll) && scroll != 0) SetDistance(distance - scroll * Safe(zoomUnitsPerNotch, 1.25f, 0, 100));
    }
    void UpdateSpeed()
    {
        Vector3 position = target.position;
        float dt = Time.fixedDeltaTime;
        currentSpeed = Finite(position) && Finite(dt) && dt > 0
            ? ((position - lastTargetPosition) / dt).magnitude : 0;
        if(!Finite(currentSpeed)) currentSpeed = 0;
        lastTargetPosition = position;
    }
    void UpdateFOV()
    {
        // Restore the original 0..speedThreshold ramp, including grounded travel.
        float normal = Safe(baseFOV, 60, 1, 179), ceiling = Safe(maxFOV, 90, normal, 179);
        float ratio = Mathf.Clamp01(currentSpeed / Safe(speedThreshold, 10, .01f, 10000));
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(normal, ceiling, ratio),
            Time.fixedDeltaTime * Safe(fovSmoothSpeed, 3, 0, 1000));
    }
    void UpdateCamera(float dt, bool snap)
    {
        float wanted = DesiredDistance();
        if(snap) smoothedZoomDistance = wanted;
        else if(dt > 0) smoothedZoomDistance = Mathf.SmoothDamp(smoothedZoomDistance, wanted, ref zoomVelocity,
            Safe(zoomSmoothTime, .08f, .01f, 10), Mathf.Infinity, dt);
        var rotation = snap || dt > 0 ? Quaternion.Euler(currentY, currentX, 0) : transform.rotation;
        Vector3 authoredPivot = target.position + offset, direction = rotation * Vector3.back;
        if(!Finite(authoredPivot)) return;
        if(!cameraCollisionEnabled)
        {
            // Explicit user choice: no camera casts, overlaps or obstruction retraction.
            currentDistance = smoothedZoomDistance;
            recoveringFromCollision = false; hasSafePosition = false; pivotAdjustment = Vector3.zero;
            Vector3 unobstructed = authoredPivot + direction * currentDistance;
            if(!snap) unobstructed = dt > 0
                ? Vector3.Lerp(transform.position, unobstructed, dt * Safe(positionSmoothing, 8, 0, 1000))
                : transform.position;
            transform.SetPositionAndRotation(unobstructed, rotation);
            return;
        }
        var physics = target.gameObject.scene.GetPhysicsScene();
        if(!physics.IsValid()) return;
        float radius = CameraRadius();
        Vector3 pivot;
        if(!TryResolvePivot(physics, authoredPivot, radius, out pivot))
        {
            // An embedded pivot/saturated query is not permission to place the
            // camera through geometry. Keep a prior clear position if possible.
            if(hasSafePosition && !SolidAt(physics, lastSafePosition, radius))
                transform.SetPositionAndRotation(lastSafePosition, rotation);
            recoveringFromCollision = true; return;
        }
        Vector3 resolvedPivot = pivot, adjustment = resolvedPivot - authoredPivot;
        float recoveryBlend = snap ? 1 : 1 - Mathf.Exp(-Safe(positionSmoothing, 8, .01f, 1000) * dt);
        // Obstructed shoulder paths retract immediately, but must not pop
        // sideways back to the full authored offset when a wall clears.
        pivotAdjustment = snap || adjustment.sqrMagnitude >= pivotAdjustment.sqrMagnitude
            ? adjustment : Vector3.Lerp(pivotAdjustment, adjustment, recoveryBlend);
        if(adjustment == Vector3.zero && pivotAdjustment.sqrMagnitude < .000001f) pivotAdjustment = Vector3.zero;
        pivot = authoredPivot + pivotAdjustment;
        if(SolidAt(physics, pivot, radius)) { pivot = resolvedPivot; pivotAdjustment = adjustment; }
        float allowed = CheckCollision(physics, pivot, direction, smoothedZoomDistance, radius);
        bool blocked = allowed < smoothedZoomDistance - .001f;
        if(snap) currentDistance = allowed;
        else if(allowed < currentDistance) currentDistance = allowed; // Immediate inward safety, including rapid turns.
        else if(blocked || recoveringFromCollision)
            currentDistance = Mathf.Lerp(currentDistance, allowed, 1 - Mathf.Exp(-Safe(positionSmoothing, 8, .01f, 1000) * dt));
        else currentDistance = allowed; // Unobstructed zoom has only its own damping.
        recoveringFromCollision = blocked || Mathf.Abs(currentDistance - smoothedZoomDistance) > .01f;
        currentDistance = Mathf.Clamp(currentDistance, 0, allowed);
        Vector3 position = pivot + direction * currentDistance;
        // Restore the old fixed-step positional easing. Collision is resolved
        // AFTER easing too: interpolating two clear endpoints can cross a corner.
        if(!snap) position = dt > 0
            ? Vector3.Lerp(transform.position, position, dt * Safe(positionSmoothing, 8, 0, 1000))
            : transform.position;
        Vector3 followDelta = position - pivot;
        float followLength = followDelta.magnitude;
        Vector3 followDirection = followLength > .0001f ? followDelta / followLength : direction;
        float clearLength = CheckCollision(physics, pivot, followDirection, followLength, radius);
        if(clearLength < followLength)
        { position = pivot + followDirection * clearLength; recoveringFromCollision = true; }
        // Casts cannot reliably report an initially overlapping sphere. Check
        // the actual eased position too, retracting to the already-clear pivot.
        if(SolidAt(physics, position, radius))
        {
            float low = 0, high = clearLength;
            for(int i = 0; i < 10; i++)
            {
                float middle = (low + high) * .5f;
                if(SolidAt(physics, pivot + followDirection * middle, radius)) high = middle; else low = middle;
            }
            currentDistance = Mathf.Min(currentDistance, low);
            position = pivot + followDirection * low; recoveringFromCollision = true;
        }
        transform.SetPositionAndRotation(position, rotation);
        lastSafePosition = position; hasSafePosition = true;
    }
    bool TryResolvePivot(PhysicsScene physics, Vector3 authored, float radius, out Vector3 pivot)
    {
        pivot = authored;
        Vector3 start = playerController != null ? playerController.transform.TransformPoint(playerController.center) : target.position;
        if(SolidAt(physics, start, radius)) return !SolidAt(physics, authored, radius);
        Vector3 delta = authored - start; float length = delta.magnitude;
        if(length > .0001f)
        {
            float clearance = CheckCollision(physics, start, delta / length, length, radius);
            pivot = start + delta / length * clearance;
        }
        // The authored offset is never modified. Only an actual obstruction may
        // temporarily shorten the path to that pivot; an open view uses it exactly.
        return !SolidAt(physics, pivot, radius);
    }
    float CheckCollision(PhysicsScene physics, Vector3 origin, Vector3 direction, float wanted, float radius)
    {
        float buffer = Safe(collisionBuffer, .3f, 0, 10);
        int count = physics.SphereCast(origin, radius, direction, hits, wanted + buffer, collisionLayers, QueryTriggerInteraction.Ignore);
        if(count == hits.Length) return 0; // Fail closed on an incomplete result set.
        float result = wanted;
        for(int i = 0; i < count; i++)
        {
            if(Ignore(hits[i].collider)) continue;
            if(!Finite(hits[i].distance)) return 0;
            result = Mathf.Min(result, Mathf.Max(0, hits[i].distance - buffer));
        }
        // The player's requested min zoom is not a physical-clearance minimum.
        return result;
    }
    bool SolidAt(PhysicsScene physics, Vector3 point, float radius)
    {
        int count = physics.OverlapSphere(point, radius, overlaps, collisionLayers, QueryTriggerInteraction.Ignore);
        if(count == overlaps.Length) return true;
        for(int i = 0; i < count; i++) if(!Ignore(overlaps[i])) return true;
        return false;
    }
    bool Ignore(Collider collider)
    {
        return collider == null || collider.transform.IsChildOf(transform) ||
            (ignoredRoot != null && collider.transform.IsChildOf(ignoredRoot));
    }
    float CameraRadius()
    {
        float radius = Safe(collisionRadius, .3f, .01f, 10);
        if(cam == null) return radius;
        float halfHeight = cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
        float nearPlaneRadius = Mathf.Sqrt(cam.nearClipPlane * cam.nearClipPlane + halfHeight * halfHeight * (1 + cam.aspect * cam.aspect));
        return Finite(nearPlaneRadius) ? Mathf.Max(radius, nearPlaneRadius) : radius;
    }
    float DesiredDistance()
    {
        float minimum = Safe(minDistance, 2, 0, 10000), maximum = Safe(maxDistance, 12, minimum, 10000);
        return Safe(distance, 6, minimum, maximum);
    }
    static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
    static float Safe(float value, float fallback, float min, float max) { return Mathf.Clamp(Finite(value) ? value : fallback, min, max); }
    public void SetTarget(Transform newTarget) { target = newTarget; ResetAfterTeleport(); }
    public void SetDistance(float newDistance) { distance = newDistance; distance = DesiredDistance(); }
    public void SetBaseFOV(float newBaseFOV) { baseFOV = newBaseFOV; }
    public void SetMaxFOV(float newMaxFOV) { maxFOV = newMaxFOV; }
}
