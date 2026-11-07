using UnityEngine;

/// <summary>One render-time orbit owner. Authored world-space offset and look/FOV
/// tuning are retained; collision and zoom never write the player or physics state.</summary>
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
    public float rotationSmoothing = 5f; // Serialized compatibility; orbit remains direct.
    [Tooltip("Collision recovery rate. Player translation follows directly, without a second positional lag.")]
    public float positionSmoothing = 8f;

    [Header("Collision")]
    public LayerMask collisionLayers = -1;
    public float collisionBuffer = .3f;
    [Tooltip("Minimum camera sphere radius; enlarged if necessary to protect near-plane corners.")]
    [Min(.01f)] public float collisionRadius = .3f;

    [Header("Speed FOV")]
    public bool speedFovEnabled = true;
    public float baseFOV = 60f;
    [Tooltip("Safety ceiling; high-speed FOV is base FOV + Speed FOV Increase, up to this limit.")]
    public float maxFOV = 90f;
    [Min(0)] public float fovStartSpeed = 20f;
    [Min(.1f)] public float fovFullSpeed = 65f;
    [Range(0, 20)] public float speedFovIncrease = 10f;
    public float fovSmoothSpeed = 3f;
    [HideInInspector] public float speedThreshold = 10f; // Legacy scene serialization; no longer the FOV curve.

    [Header("Peripheral Speed Streaks")]
    public bool speedStreaksEnabled = true;
    [Min(0)] public float streakStartSpeed = 35f;
    [Min(.1f)] public float streakFullSpeed = 65f;
    [Range(0, .35f)] public float streakOpacity = .16f;
    [Tooltip("Fraction of each screen edge occupied by streaks; the centre stays clear.")]
    [Range(.05f, .22f)] public float streakEdgeBand = .14f;
    [Min(.01f)] public float streakFadeIn = .2f;
    [Min(.01f)] public float streakFadeOut = .15f;
    [Tooltip("Reject faster displacement for presentation only; never caps player velocity.")]
    [Min(1)] public float maximumFeedbackSpeed = 160f;

    float currentX, currentY, currentDistance, smoothedZoomDistance, zoomVelocity;
    Camera cam;
    Vector3 lastTargetPosition;
    float currentSpeed;
    Transform observedTarget, ignoredRoot;
    CharacterController playerController;
    bool recoveringFromCollision, hasSafePosition;
    Vector3 lastSafePosition, pivotAdjustment;
    PlayerHealth speedHealth;
    Movement walking;
    ODM_Movement gear;
    ScoutPlayerAnimation scout;
    TitanODMMotor motor;
    TitanCombatTarget combatTarget;
    PlayerRespawn respawn;
    PlayerSpeedStreaks streaks;
    Vector3 sampledVelocity;
    float speedSampledAt, streakAmount;
    int validSpeedSamples;
    bool speedPositioned, focusLost, applicationPaused;
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
        speedHealth = health;
        walking = ignoredRoot.GetComponent<Movement>(); gear = ignoredRoot.GetComponent<ODM_Movement>();
        scout = ignoredRoot.GetComponent<ScoutPlayerAnimation>(); motor = ignoredRoot.GetComponent<TitanODMMotor>();
        combatTarget = ignoredRoot.GetComponent<TitanCombatTarget>(); respawn = ignoredRoot.GetComponent<PlayerRespawn>();
        ClearSpeedFeedback();
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
        if(target == null || cam == null) return;
        if(observedTarget != target) ResetAfterTeleport();
        // Measure after the existing motors; render-frame displacement would
        // alternate between zero and inflated speed at high frame rates.
        UpdateSpeed();
    }
    void LateUpdate()
    {
        if(target == null || cam == null) { ClearSpeedFeedback(); return; }
        if(observedTarget != target) ResetAfterTeleport();
        if(Time.timeScale <= 0) { ClearSpeedFeedback(); return; }
        if(Time.deltaTime > 0 && Finite(Time.deltaTime))
        {
            UpdateSpeedFeedback(Time.deltaTime); // Lens is final before near-plane collision sizing.
            UpdateCamera(Time.deltaTime, false);
        }
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
    void RefreshSpeedReferences()
    {
        if(ignoredRoot == null) return;
        if(scout == null) scout = ignoredRoot.GetComponent<ScoutPlayerAnimation>();
        if(motor == null) motor = ignoredRoot.GetComponent<TitanODMMotor>();
        if(combatTarget == null) combatTarget = ignoredRoot.GetComponent<TitanCombatTarget>();
        if(respawn == null) respawn = ignoredRoot.GetComponent<PlayerRespawn>();
    }
    bool SpeedFeedbackBlocked()
    {
        return !isActiveAndEnabled || focusLost || applicationPaused || Time.timeScale <= 0 ||
            cam == null || !cam.isActiveAndEnabled || cam.targetTexture != null ||
            speedHealth == null || !speedHealth.isActiveAndEnabled || speedHealth.isDead || speedHealth.isGrabbed ||
            playerController == null || !playerController.enabled || walking == null || gear == null ||
            !walking.isActiveAndEnabled || !gear.isActiveAndEnabled || walking.TitanSuspended || gear.TitanSuspended ||
            (motor != null && motor.Suspended) || (combatTarget != null && (combatTarget.IsHeld || combatTarget.ReleasePending)) ||
            (respawn != null && respawn.Pending);
    }
    void ClearSpeedFeedback()
    {
        currentSpeed = 0; sampledVelocity = Vector3.zero; validSpeedSamples = 0; speedPositioned = false;
        lastTargetPosition = ignoredRoot != null ? ignoredRoot.position : target != null ? target.position : Vector3.zero;
        speedSampledAt = Time.time; streakAmount = 0;
        if(streaks != null) streaks.Hide();
        if(cam != null) cam.fieldOfView = NeutralFOV();
    }
    void UpdateSpeed()
    {
        RefreshSpeedReferences();
        Vector3 position = ignoredRoot != null ? ignoredRoot.position : target.position;
        float dt = Time.fixedDeltaTime;
        if(SpeedFeedbackBlocked() || !Finite(position) || !Finite(dt) || dt <= 0 || dt > .1f)
        { ClearSpeedFeedback(); return; }
        if(!speedPositioned)
        { lastTargetPosition = position; speedPositioned = true; speedSampledAt = Time.time; return; }
        Vector3 velocity = (position - lastTargetPosition) / dt;
        lastTargetPosition = position; speedSampledAt = Time.time;
        if(!Finite(velocity) || velocity.magnitude > Safe(maximumFeedbackSpeed, 160, 1, 10000))
        { ClearSpeedFeedback(); return; }
        validSpeedSamples = Mathf.Min(2, validSpeedSamples + 1);
        sampledVelocity = validSpeedSamples >= 2 ? velocity : Vector3.zero;
        currentSpeed = sampledVelocity.magnitude;
    }
    void UpdateSpeedFeedback(float dt)
    {
        RefreshSpeedReferences();
        if(SpeedFeedbackBlocked() || !Finite(Time.unscaledDeltaTime) || Time.unscaledDeltaTime > .2f ||
            Time.time - speedSampledAt > .12f)
        { ClearSpeedFeedback(); return; }
        if(validSpeedSamples < 2)
        {
            streakAmount = 0; if(streaks != null) streaks.Hide(); cam.fieldOfView = NeutralFOV(); return;
        }
        bool airborne = !playerController.isGrounded && !(scout != null && scout.isActiveAndEnabled && scout.Grounded);
        float fovAmount = speedFovEnabled && airborne ? SpeedAmount(currentSpeed, fovStartSpeed, fovFullSpeed, 20, 65) : 0;
        float normal = NeutralFOV(), ceiling = Safe(maxFOV, 90, normal, 179);
        float fastFOV = Mathf.Min(ceiling, normal + Safe(speedFovIncrease, 10, 0, 20));
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(normal, fastFOV, fovAmount),
            1 - Mathf.Exp(-Safe(fovSmoothSpeed, 3, .01f, 1000) * dt));
        float wanted = speedStreaksEnabled && airborne ? SpeedAmount(currentSpeed, streakStartSpeed, streakFullSpeed, 35, 65) : 0;
        float fade = wanted > streakAmount ? Safe(streakFadeIn, .2f, .01f, 10) : Safe(streakFadeOut, .15f, .01f, 10);
        streakAmount = Mathf.MoveTowards(streakAmount, wanted, dt / fade);
        float opacity = Safe(streakOpacity, .16f, 0, .35f);
        if(streaks == null && streakAmount > .001f && opacity > 0) streaks = PlayerSpeedStreaks.Create(cam);
        if(streaks != null)
        {
            // Orbit may change this frame; flow follows movement in the final orbit frame, not the old camera pose.
            Vector3 direction = Quaternion.Inverse(Quaternion.Euler(currentY, currentX, 0)) * sampledVelocity.normalized;
            streaks.Present(streakAmount, direction, dt, opacity, Safe(streakEdgeBand, .14f, .05f, .22f), cam.rect);
            if(streaks.Overlay != null) streaks.Overlay.targetDisplay = cam.targetDisplay;
        }
    }
    float NeutralFOV() { return Safe(baseFOV, 60, 1, 179); }
    static float SpeedAmount(float speed, float start, float full, float defaultStart, float defaultFull)
    {
        start = Safe(start, defaultStart, 0, 10000); full = Safe(full, defaultFull, start + .1f, 10001);
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, full, speed));
    }
    void OnApplicationFocus(bool focused) { focusLost = !focused; if(!focused) ClearSpeedFeedback(); }
    void OnApplicationPause(bool paused) { applicationPaused = paused; if(paused) ClearSpeedFeedback(); }
    void OnDisable() { ClearSpeedFeedback(); }
    void OnDestroy() { if(streaks != null && streaks.Overlay != null) Destroy(streaks.Overlay.gameObject); }
    void UpdateCamera(float dt, bool snap)
    {
        float wanted = DesiredDistance();
        smoothedZoomDistance = snap ? wanted : Mathf.SmoothDamp(smoothedZoomDistance, wanted, ref zoomVelocity,
            Safe(zoomSmoothTime, .08f, .01f, 10), Mathf.Infinity, dt);
        var rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 authoredPivot = target.position + offset, direction = rotation * Vector3.back;
        if(!Finite(authoredPivot)) return;
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
        // Casts cannot reliably report an initially overlapping sphere. Check
        // the final volume too, retracting toward the already-clear pivot.
        if(SolidAt(physics, position, radius))
        {
            float low = 0, high = currentDistance;
            for(int i = 0; i < 10; i++)
            {
                float middle = (low + high) * .5f;
                if(SolidAt(physics, pivot + direction * middle, radius)) high = middle; else low = middle;
            }
            currentDistance = low; position = pivot + direction * low; recoveringFromCollision = true;
        }
        // Follow the actual target after movement/holds/reactions, rather than
        // independently lerping its translation and accumulating flight-speed lag.
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
