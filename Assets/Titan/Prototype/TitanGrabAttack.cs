using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public enum TitanGrabStage { Idle, Windup, Reach, Holding, Recovery }

/// <summary>Standing sandbox grab: fixed aim, one contact event, exclusive bounded hold.
/// No bite, damage, legacy player controller, or delayed coroutine can kill the victim.</summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ModularTitanBody))]
public class TitanGrabAttack : MonoBehaviour
{
    public bool automaticTick = true;
    public bool drawTelegraph = true;
    [Min(.3f)] public float windupDuration = .65f;
    [Min(.1f)] public float reachDuration = .18f;
    [Min(.3f)] public float holdDuration = 1.5f;
    [Min(.2f)] public float recoveryDuration = .45f;
    [Min(.2f)] public float cooldownDuration = 3;
    [Min(1)] public float attackRange = 6.4f;
    [Min(.1f)] public float contactRadius = 1.05f;
    public LayerMask sceneryMask = ~0, victimMask = ~0;
    public TitanGrabStage Stage { get; private set; }
    public bool IsBusy { get { return Stage != TitanGrabStage.Idle; } }
    public bool UsesLeftHand { get; private set; }
    public Vector3 LockedPoint { get; private set; }
    public Vector3 LastContactPoint { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float Elapsed { get { return elapsed; } }
    public int ContactCount { get; private set; }
    public int CaptureCount { get; private set; }
    public bool LastQueryOverflow { get; private set; }
    public string LastResult { get; private set; }

    ModularTitanBody body;
    TitanPatrolChaseAI brain;
    NavMeshAgent agent;
    TitanCombatTarget victim;
    Transform upper, lower, hand, grip, spine, chest, neck;
    Transform[] bones;
    Pose[] snapshots;
    Transform[] legBones = new Transform[6];
    Vector3[] plants = new Vector3[2];
    Quaternion[] flats = new Quaternion[2];
    readonly Vector3[] baseFeet = new Vector3[2];
    readonly Quaternion[] baseRotations = new Quaternion[2];
    float elapsed, windup, reach, holding, recovery, radius;
    bool contacted, captured;
    int beginFrame = -1;
    Vector3 contactGrip, selectionOrigin;
    readonly RaycastHit[] rays = new RaycastHit[64];
    readonly Collider[] overlaps = new Collider[64];
    LineRenderer cue;
    Material cueMaterial;

    void Awake()
    {
        body = GetComponent<ModularTitanBody>(); brain = GetComponent<TitanPatrolChaseAI>(); agent = GetComponent<NavMeshAgent>();
        bones = body.hips != null ? body.hips.GetComponentsInChildren<Transform>() : new Transform[0];
        snapshots = new Pose[bones.Length];
        foreach (var bone in bones)
        {
            if (bone.name == "Spine") spine = bone;
            if (bone.name == "Chest") chest = bone;
            if (bone.name == "Neck") neck = bone;
        }
        LastResult = "Ready";
    }
    void OnEnable() { body.Died += OnDied; body.BlindnessChanged += OnBlind; body.LimbSevered += OnSevered; }
    void OnDisable()
    {
        Cancel("Disabled");
        body.Died -= OnDied; body.BlindnessChanged -= OnBlind; body.LimbSevered -= OnSevered;
    }
    void OnDestroy() { if (cueMaterial != null) Destroy(cueMaterial); }
    void OnDied() { Cancel("Dead"); }
    void OnBlind(bool blind) { if (blind) Cancel("Blinded"); }
    void OnSevered(TitanHitRegion region)
    {
        if (region == TitanHitRegion.LeftLeg || region == TitanHitRegion.RightLeg ||
            region == (UsesLeftHand ? TitanHitRegion.LeftArm : TitanHitRegion.RightArm)) Cancel("Required limb lost");
    }
    void OnReleased() { if (IsBusy && captured) Cancel("Victim released"); }
    void Update()
    {
        // Director/input may start us earlier in THIS Update. Its delta belongs to
        // the preceding interval, not to the new wind-up (especially on long frames).
        if (automaticTick && Time.frameCount != beginFrame) TickAttack(Time.deltaTime);
    }
    void LateUpdate() { ApplyPose(); }

    public bool TryBeginGrab(TitanCombatTarget target, bool leftHand)
    {
        if (!isActiveAndEnabled || IsBusy || CooldownRemaining > 0 || body.hips == null || body.head == null ||
            !body.CanWalk || body.animator == null || !body.animator.enabled || !(leftHand ? body.CanGrabLeft : body.CanGrabRight) ||
            target == null || target.IsDead || target.IsHeld || !target.isActiveAndEnabled || target.gameObject.scene != gameObject.scene) return false;
        var stomp = GetComponent<TitanStompAttack>();
        if (stomp != null && stomp.IsBusy) return false;
        if (brain != null && (!brain.isActiveAndEnabled || brain.target != target.transform || !brain.CanSeeTarget || brain.State != TitanAwarenessState.Chase)) return false;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 1) return false;
        Vector3 delta = target.transform.position - transform.position;
        if (Vector3.ProjectOnPlane(delta, Vector3.up).magnitude > attackRange || Mathf.Abs(delta.y) > 2.2f ||
            Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(delta, Vector3.up)) > 65 ||
            !ClearRay(body.head.position, target.transform.position + Vector3.up, target.transform)) return false;
        upper = leftHand ? body.leftUpperArm : body.rightUpperArm;
        lower = leftHand ? body.leftLowerArm : body.rightLowerArm;
        hand = leftHand ? body.leftHand : body.rightHand;
        grip = leftHand ? body.leftGripSocket : body.rightGripSocket;
        if (upper == null || lower == null || hand == null || grip == null || !grip.IsChildOf(transform) || !CachePlants()) return false;
        if (brain != null && !brain.TryAcquireMovementLock(this)) return false;
        LockedPoint = target.transform.position + Vector3.up; selectionOrigin = body.head.position;
        victim = target; UsesLeftHand = leftHand; elapsed = 0; contacted = captured = false; LastQueryOverflow = false;
        windup = Mathf.Max(.3f, windupDuration); reach = Mathf.Max(.1f, reachDuration);
        holding = Mathf.Clamp(holdDuration, .3f, 3); recovery = Mathf.Max(.2f, recoveryDuration); radius = Mathf.Max(.1f, contactRadius);
        Stage = TitanGrabStage.Windup; beginFrame = Time.frameCount; LastResult = "Wind-up / aim locked";
        body.SetWalking(false);
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
        UpdateCue(); return true;
    }

    public void TickAttack(float deltaTime)
    {
        if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        CooldownRemaining = Mathf.Max(0, CooldownRemaining - deltaTime);
        if (!IsBusy) return;
        if (!body.CanWalk || body.animator == null || !body.animator.enabled || !(UsesLeftHand ? body.CanGrabLeft : body.CanGrabRight) ||
            victim == null || !victim.isActiveAndEnabled || victim.IsDead ||
            (captured && Stage != TitanGrabStage.Recovery && !victim.IsHeldBy(this)) || (!contacted && victim.IsHeld) ||
            (brain != null && (!brain.isActiveAndEnabled || brain.target != victim.transform))) { Cancel("Interrupted"); return; }
        elapsed += deltaTime;
        if (!contacted && elapsed >= windup + reach)
        {
            float now = elapsed; elapsed = windup + reach; Stage = TitanGrabStage.Reach;
            ResolveContact(); elapsed = now;
        }
        if (!IsBusy) return;
        float recoveryStart = windup + reach + (captured ? holding : 0);
        if (elapsed < windup) Stage = TitanGrabStage.Windup;
        else if (elapsed < windup + reach) Stage = TitanGrabStage.Reach;
        else if (captured && elapsed < recoveryStart) Stage = TitanGrabStage.Holding;
        else
        {
            ReleaseVictim("Hold complete"); Stage = TitanGrabStage.Recovery;
            if (elapsed >= recoveryStart + recovery) Finish("Recovered");
        }
        UpdateCue();
    }
    void ResolveContact()
    {
        contacted = true; ContactCount++;
        if (!ClearRay(selectionOrigin, LockedPoint, victim.transform)) { LastResult = "Reach blocked"; return; }
        // Evaluate the exact contact pose even on a long frame. Restore the rendered
        // pose afterwards: do not accumulate an overlay or rewind Animator phase.
        for (int i = 0; i < bones.Length; i++)
            if (Attached(bones[i])) snapshots[i] = new Pose(bones[i].localPosition, bones[i].localRotation);
        try
        {
            body.animator.Update(0);
            float contactTime = elapsed;
            elapsed = windup; ApplyPose(); Vector3 from = grip.position;
            body.animator.Update(0); elapsed = contactTime; ApplyPose(); contactGrip = LastContactPoint = grip.position;
            Physics.SyncTransforms();
            Vector3 sweep = contactGrip - from;
            int swept = sweep.sqrMagnitude > .0001f ? gameObject.scene.GetPhysicsScene().SphereCast(from, radius, sweep.normalized, rays, sweep.magnitude, sceneryMask, QueryTriggerInteraction.Ignore) : 0;
            if (swept == rays.Length) { LastQueryOverflow = true; LastResult = "Sweep saturated / miss"; return; }
            for (int i = 0; i < swept; i++)
                if (rays[i].transform.gameObject.scene == gameObject.scene && !Attached(rays[i].transform) && !rays[i].transform.IsChildOf(victim.transform)) { LastResult = "Reach blocked"; return; }
            int solids = gameObject.scene.GetPhysicsScene().OverlapSphere(contactGrip, radius, overlaps, sceneryMask, QueryTriggerInteraction.Ignore);
            if (solids == overlaps.Length) { LastQueryOverflow = true; LastResult = "Contact scenery saturated / miss"; return; }
            for (int i = 0; i < solids; i++)
                if (overlaps[i].gameObject.scene == gameObject.scene && !Attached(overlaps[i].transform) && !overlaps[i].transform.IsChildOf(victim.transform)) { LastResult = "Contact blocked"; return; }
            int count = gameObject.scene.GetPhysicsScene().OverlapSphere(contactGrip, radius, overlaps, victimMask, QueryTriggerInteraction.Ignore);
            LastQueryOverflow = count == overlaps.Length;
            if (LastQueryOverflow) { LastResult = "Contact query saturated / miss"; return; }
            for (int i = 0; i < count; i++)
            {
                var col = overlaps[i];
                if (col.GetComponentInParent<TitanCombatTarget>() != victim || !ClearRay(contactGrip, col.bounds.center, victim.transform)) continue;
                TryCapture(); break;
            }
            // Unity CCT scene queries shrink its capsule. Test the configured shape
            // too, so a real controller is not harder to hit than the Box stand-in.
            var controller = victim.GetComponent<CharacterController>();
            if (!captured && controller != null && controller.enabled && (victimMask.value & (1 << controller.gameObject.layer)) != 0 &&
                (TitanCombatTarget.ClosestCombatPoint(controller, contactGrip) - contactGrip).sqrMagnitude <= radius * radius &&
                ClearRay(contactGrip, controller.bounds.center, victim.transform)) TryCapture();
            if (!captured) LastResult = "Missed";
        }
        finally
        {
            for (int i = 0; i < bones.Length; i++)
                if (Attached(bones[i])) { bones[i].localPosition = snapshots[i].position; bones[i].localRotation = snapshots[i].rotation; }
            Physics.SyncTransforms();
        }
    }
    void TryCapture()
    {
        if (!victim.TryHold(this, grip)) return;
        captured = true; CaptureCount++; victim.HoldReleased += OnReleased; LastResult = "Captured / E to escape";
    }
    public void Cancel(string reason = "Cancelled") { if (IsBusy) Finish(reason); }
    void Finish(string reason)
    {
        Stage = TitanGrabStage.Idle; LastResult = reason; CooldownRemaining = Mathf.Max(.2f, cooldownDuration);
        ReleaseVictim(reason); victim = null;
        if (brain != null) brain.ReleaseMovementLock(this);
        if (cue != null) cue.enabled = false;
        // No pose reset: injury/death owns the current surviving bones.
    }
    void ReleaseVictim(string reason)
    {
        if (victim == null) return;
        victim.HoldReleased -= OnReleased; victim.ReleaseHold(this, reason);
    }

    public void ApplyPose()
    {
        if (!IsBusy || !body.CanWalk || !Attached(upper) || !Attached(grip)) return;
        float contactTime = windup + reach;
        float recoveryStart = contactTime + (captured ? holding : 0);
        float recover = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - recoveryStart) / recovery));
        float carry = captured ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - contactTime) / .55f)) : 0;
        float load = Mathf.SmoothStep(0, 1, elapsed / windup) * (1 - recover) * (1 - carry * .45f);
        baseFeet[0] = legBones[2].position; baseFeet[1] = legBones[5].position;
        baseRotations[0] = legBones[2].rotation; baseRotations[1] = legBones[5].rotation;
        Vector3 toward = Vector3.ProjectOnPlane(LockedPoint - transform.position, Vector3.up).normalized;
        body.hips.position += (toward * 2.6f - Vector3.up * 3.8f) * load;
        Tilt(spine, 14 * load); Tilt(chest, 18 * load); Tilt(neck, 10 * load); Tilt(body.head, 15 * load);
        for (int i = 0; i < 2; i++)
        {
            Solve(legBones[i*3], legBones[i*3+1], legBones[i*3+2], Vector3.Lerp(plants[i], baseFeet[i], recover), transform.forward, 1);
            legBones[i*3+2].rotation = Quaternion.Slerp(flats[i], baseRotations[i], recover);
        }
        float side = UsesLeftHand ? -1 : 1;
        Vector3 windupPoint = LockedPoint + Vector3.up * 2 + transform.right * side * 1.1f - toward * .45f;
        Vector3 aim = Vector3.Lerp(windupPoint, LockedPoint, Mathf.SmoothStep(0, 1, (elapsed - windup) / reach));
        if (captured) aim = Vector3.Lerp(contactGrip, transform.position + Vector3.up * 6.2f + transform.forward * 2 + transform.right * side * 1.2f, carry);
        Solve(upper, lower, grip, aim, transform.right * side + transform.forward * .3f, (1 - recover) * Mathf.Clamp01(elapsed / .2f));
        Transform otherUpper = UsesLeftHand ? body.rightUpperArm : body.leftUpperArm;
        Transform otherLower = UsesLeftHand ? body.rightLowerArm : body.leftLowerArm;
        if (Attached(otherUpper) && otherLower != null)
            Aim(otherUpper, otherLower, otherUpper.position - Vector3.up * 2 - transform.right * side * 1.6f, load * .65f);
    }
    bool CachePlants()
    {
        for (int i = 0; i < 2; i++)
        {
            TitanDetachableLimb leg = null;
            foreach (var item in body.limbs)
                if (!item.severed && item.region == (i == 0 ? TitanHitRegion.LeftLeg : TitanHitRegion.RightLeg)) leg = item;
            if (leg == null || leg.bodies.Length < 3) return false;
            for (int j = 0; j < 3; j++) legBones[i*3+j] = leg.bodies[j].transform;
            var ankle = legBones[i*3+2]; BoxCollider shape = null;
            foreach (var box in ankle.GetComponentsInChildren<BoxCollider>()) if (!box.isTrigger && box.attachedRigidbody == leg.bodies[2]) { shape = box; break; }
            if (shape == null) return false;
            flats[i] = transform.rotation * Quaternion.Inverse(Quaternion.Inverse(ankle.rotation) * shape.transform.rotation);
            int count = gameObject.scene.GetPhysicsScene().Raycast(ankle.position + Vector3.up * 4, Vector3.down, rays, 10, sceneryMask, QueryTriggerInteraction.Ignore);
            if (count == rays.Length) return false;
            float ground = float.NegativeInfinity;
            for (int j = 0; j < count; j++)
                if (!Attached(rays[j].transform) && rays[j].rigidbody == null && rays[j].normal.y > .65f &&
                    rays[j].point.y <= transform.position.y + 1.2f && rays[j].transform.GetComponentInParent<TitanCombatTarget>() == null)
                    ground = Mathf.Max(ground, rays[j].point.y);
            if (float.IsNegativeInfinity(ground)) return false;
            Vector3 offset = Quaternion.Inverse(ankle.rotation) * (shape.transform.TransformPoint(shape.center) - ankle.position);
            plants[i] = new Vector3(ankle.position.x, ground + shape.size.y * Mathf.Abs(shape.transform.lossyScale.y) * .5f + .035f, ankle.position.z) - flats[i] * offset;
        }
        return true;
    }
    bool Attached(Transform bone) { return bone != null && bone.IsChildOf(transform); }
    void Tilt(Transform bone, float angle) { if (Attached(bone)) bone.rotation = Quaternion.AngleAxis(angle, transform.right) * bone.rotation; }
    static void Solve(Transform a, Transform b, Transform end, Vector3 target, Vector3 pole, float weight)
    {
        float first = Vector3.Distance(a.position, b.position), second = Vector3.Distance(b.position, end.position);
        Vector3 direction = target - a.position;
        float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(first-second)+.01f, first+second-.01f);
        direction.Normalize();
        Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
        if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(Vector3.up, direction).normalized;
        float along = (first*first + distance*distance - second*second) / (2*distance);
        Vector3 middle = a.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
        Aim(a, b, middle, weight); Aim(b, end, target, weight);
    }
    static void Aim(Transform bone, Transform child, Vector3 target, float weight)
    {
        Vector3 from = child.position - bone.position, to = target - bone.position;
        if (from.sqrMagnitude > .0001f && to.sqrMagnitude > .0001f)
            bone.rotation = Quaternion.Slerp(bone.rotation, Quaternion.FromToRotation(from, to) * bone.rotation, weight);
    }
    bool ClearRay(Vector3 from, Vector3 to, Transform allowed)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < .0001f) return true;
        int count = gameObject.scene.GetPhysicsScene().Raycast(from, delta.normalized, rays, delta.magnitude, sceneryMask, QueryTriggerInteraction.Ignore);
        if (count == rays.Length) return false;
        for (int i = 0; i < count; i++)
            if (rays[i].transform.gameObject.scene == gameObject.scene && !Attached(rays[i].transform) && !rays[i].transform.IsChildOf(allowed)) return false;
        return true;
    }
    void UpdateCue()
    {
        bool visible = drawTelegraph && (Stage == TitanGrabStage.Windup || Stage == TitanGrabStage.Reach);
        if (!visible) { if (cue != null) cue.enabled = false; return; }
        if (cue == null)
        {
            var go = new GameObject("Grab fixed aim telegraph"); go.transform.SetParent(transform, false);
            cue = go.AddComponent<LineRenderer>(); cue.loop = true; cue.useWorldSpace = true; cue.positionCount = 48; cue.widthMultiplier = .08f;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit"); if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) { cueMaterial = new Material(shader); cue.sharedMaterial = cueMaterial; }
        }
        cue.enabled = true;
        Color color = Stage == TitanGrabStage.Reach ? Color.red : Color.cyan;
        cue.startColor = cue.endColor = color;
        if (cueMaterial != null) { if (cueMaterial.HasProperty("_BaseColor")) cueMaterial.SetColor("_BaseColor", color); else cueMaterial.color = color; }
        for (int i = 0; i < cue.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2 / cue.positionCount;
            cue.SetPosition(i, LockedPoint + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
        }
    }
}
