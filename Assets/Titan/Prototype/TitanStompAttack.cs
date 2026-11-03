using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public enum TitanStompStage { Idle, Windup, Strike, Recovery }

/// <summary>Opt-in ground attack. Fixed telegraph, one scene-local impact, no player dependency.
/// Animator supplies the base pose; this overlay runs after motion, before injury posing.</summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ModularTitanBody))]
public class TitanStompAttack : MonoBehaviour
{
    public bool automaticTick = true;
    public bool automaticAttack = true;
    public bool drawTelegraph = true;
    [Min(.3f)] public float windupDuration = .65f;
    [Min(.08f)] public float strikeDuration = .14f;
    [Min(.2f)] public float recoveryDuration = .5f;
    [Min(.2f)] public float cooldownDuration = 2.2f;
    [Min(1)] public float attackRange = 6.4f;
    [Min(.1f)] public float impactRadius = 2.4f;
    [Min(.1f)] public float impactHeight = 1.8f;
    [Min(0)] public float damage = 40;
    public LayerMask sceneryMask = ~0;
    public LayerMask victimMask = ~0;
    public TitanStompStage Stage { get; private set; }
    public bool IsBusy { get { return Stage != TitanStompStage.Idle; } }
    public bool UsesLeftFoot { get; private set; }
    public Vector3 LockedPoint { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float Elapsed { get { return elapsed; } }
    public int ImpactCount { get; private set; }
    public bool LastQueryOverflow { get; private set; }
    public string LastResult { get; private set; }

    ModularTitanBody body;
    TitanPatrolChaseAI brain;
    NavMeshAgent agent;
    TitanCombatTarget victim;
    Transform upper, lower, foot, supportUpper, supportLower, supportFoot, chest, spine, neck;
    Quaternion footFlat, supportFlat;
    Vector3 startFoot, startSupport, landingFoot, plantedSupport;
    float elapsed, windup, strike, recovery, radius, height, attackDamage, groundY, stationaryAge;
    bool impacted;
    int sequence, beginFrame = -1;
    LineRenderer ring;
    Material ringMaterial;
    readonly RaycastHit[] rays = new RaycastHit[64];
    readonly Collider[] overlaps = new Collider[128];
    readonly HashSet<TitanCombatTarget> damaged = new HashSet<TitanCombatTarget>();

    void Awake()
    {
        body = GetComponent<ModularTitanBody>();
        brain = GetComponent<TitanPatrolChaseAI>();
        agent = GetComponent<NavMeshAgent>();
        foreach (var t in body.hips.GetComponentsInChildren<Transform>())
        {
            if (t.name == "Chest") chest = t;
            if (t.name == "Spine") spine = t;
            if (t.name == "Neck") neck = t;
        }
        LastResult = "Ready";
    }
    void OnEnable()
    {
        body.Died += OnDied;
        body.LimbSevered += OnSevered;
        body.BlindnessChanged += OnBlind;
    }
    void OnDisable()
    {
        Cancel("Disabled");
        if (body == null) return;
        body.Died -= OnDied;
        body.LimbSevered -= OnSevered;
        body.BlindnessChanged -= OnBlind;
    }
    void OnDestroy() { if (ringMaterial != null) Destroy(ringMaterial); }
    void OnDied() { Cancel("Dead"); }
    void OnBlind(bool blind) { if (blind) Cancel("Blinded"); }
    void OnSevered(TitanHitRegion region)
    {
        if (region == TitanHitRegion.LeftLeg || region == TitanHitRegion.RightLeg) Cancel("Leg lost");
    }

    void Update()
    {
        if (!automaticTick) return;
        // A director/input start this frame must receive the entire telegraph,
        // not consume the delta that elapsed BEFORE it was selected.
        if (Time.frameCount != beginFrame) TickAttack(Time.deltaTime);
        bool settled = agent == null || (agent.isActiveAndEnabled && agent.isOnNavMesh && agent.isStopped && agent.velocity.sqrMagnitude < .25f);
        stationaryAge = settled ? stationaryAge + Time.deltaTime : 0;
        if (stationaryAge < .12f) return; // Plant after braking before loading a stomp.
        if (!automaticAttack || IsBusy || brain == null || !brain.isActiveAndEnabled ||
            brain.State != TitanAwarenessState.Chase || !brain.CanSeeTarget || brain.target == null) return;
        var candidate = brain.target.GetComponent<TitanCombatTarget>();
        if (candidate == null) return;
        float side = transform.InverseTransformPoint(candidate.transform.position).x;
        bool left = Mathf.Abs(side) > .25f ? side < 0 : sequence % 2 == 0;
        TryBeginStomp(candidate, left);
    }

    public bool TryBeginStomp(TitanCombatTarget target, bool leftFoot)
    {
        if (!isActiveAndEnabled || IsBusy || CooldownRemaining > 0 || !body.CanWalk ||
            target == null || !target.isActiveAndEnabled || target.IsDead || target.IsHeld || target.gameObject.scene != gameObject.scene) return false;
        var grab = GetComponent<TitanGrabAttack>();
        if (grab != null && grab.IsBusy) return false;
        if (brain != null && (!brain.isActiveAndEnabled || brain.target != target.transform ||
            !brain.CanSeeTarget || brain.State != TitanAwarenessState.Chase)) return false;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 1) return false;
        Vector3 delta = target.transform.position - transform.position;
        Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
        if (horizontal.magnitude > attackRange || Mathf.Abs(delta.y) > 2.2f ||
            Vector3.Angle(transform.forward, horizontal) > 65 ||
            !ClearRay(body.head.position, target.transform.position + Vector3.up, target.transform)) return false;
        Vector3 point = transform.position + Vector3.ClampMagnitude(horizontal, 4.1f);
        Vector3 ground;
        if (!FindGround(point, out ground)) return false;
        TitanDetachableLimb attackLeg = FindLeg(leftFoot), otherLeg = FindLeg(!leftFoot);
        if (attackLeg == null || otherLeg == null) return false;
        if (brain != null && !brain.TryAcquireMovementLock(this)) return false;
        upper = attackLeg.bodies[0].transform; lower = attackLeg.bodies[1].transform; foot = attackLeg.bodies[2].transform;
        supportUpper = otherLeg.bodies[0].transform; supportLower = otherLeg.bodies[1].transform; supportFoot = otherLeg.bodies[2].transform;
        footFlat = FlatFoot(foot, attackLeg.bodies[2]); supportFlat = FlatFoot(supportFoot, otherLeg.bodies[2]);
        LockedPoint = ground; groundY = ground.y;
        landingFoot = SolePosition(ground, foot, attackLeg.bodies[2], footFlat);
        Vector3 supportGround;
        if (!FindGround(supportFoot.position, out supportGround)) supportGround = new Vector3(supportFoot.position.x, groundY, supportFoot.position.z);
        plantedSupport = SolePosition(supportGround, supportFoot, otherLeg.bodies[2], supportFlat);
        startFoot = foot.position; startSupport = supportFoot.position;
        victim = target; UsesLeftFoot = leftFoot;
        elapsed = 0; impacted = false; LastQueryOverflow = false;
        windup = Mathf.Max(.3f, windupDuration); strike = Mathf.Max(.08f, strikeDuration);
        recovery = Mathf.Max(.2f, recoveryDuration); radius = Mathf.Max(.1f, impactRadius);
        height = Mathf.Max(.1f, impactHeight); attackDamage = Mathf.Max(0, damage);
        Stage = TitanStompStage.Windup; beginFrame = Time.frameCount; sequence++; LastResult = "Wind-up / landing point locked";
        body.SetWalking(false);
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
        UpdateRing();
        return true;
    }

    TitanDetachableLimb FindLeg(bool left)
    {
        foreach (var limb in body.limbs)
            if (limb.region == (left ? TitanHitRegion.LeftLeg : TitanHitRegion.RightLeg) && !limb.severed && limb.bodies.Length >= 3) return limb;
        return null;
    }
    BoxCollider FootBox(Rigidbody rb)
    {
        foreach (var box in rb.GetComponentsInChildren<BoxCollider>()) if (!box.isTrigger && box.attachedRigidbody == rb) return box;
        return null;
    }
    Quaternion FlatFoot(Transform bone, Rigidbody rb)
    {
        var box = FootBox(rb);
        return box != null ? transform.rotation * Quaternion.Inverse(Quaternion.Inverse(bone.rotation) * box.transform.rotation) : bone.rotation;
    }
    Vector3 SolePosition(Vector3 ground, Transform bone, Rigidbody rb, Quaternion rotation)
    {
        var box = FootBox(rb);
        if (box == null) return ground + Vector3.up * .65f;
        Vector3 offset = Quaternion.Inverse(bone.rotation) * (box.transform.TransformPoint(box.center) - bone.position);
        float halfHeight = box.size.y * Mathf.Abs(box.transform.lossyScale.y) * .5f;
        return ground + Vector3.up * (halfHeight + .035f) - rotation * offset;
    }

    public void TickAttack(float deltaTime)
    {
        if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        CooldownRemaining = Mathf.Max(0, CooldownRemaining - deltaTime);
        if (!IsBusy) return;
        if (!body.CanWalk || (!impacted && (victim == null || !victim.isActiveAndEnabled || victim.IsDead)) ||
            (brain != null && (!brain.isActiveAndEnabled || (!impacted && brain.target != victim.transform)))) { Cancel("Interrupted"); return; }
        elapsed += deltaTime;
        if (elapsed < windup) Stage = TitanStompStage.Windup;
        else if (elapsed < windup + strike) Stage = TitanStompStage.Strike;
        else
        {
            // A long frame must cross the contact event once, not skip it.
            if (!impacted) Impact();
            if (!IsBusy) return; // Damage callbacks can interrupt/kill the attacker.
            Stage = TitanStompStage.Recovery;
            if (elapsed >= windup + strike + recovery) Finish("Recovered");
        }
        UpdateRing();
    }

    public void Cancel(string reason = "Cancelled")
    {
        if (!IsBusy) return;
        Finish(reason);
        // Never restore a standing pose here: injury/death owns the current pose.
    }
    void Finish(string result)
    {
        Stage = TitanStompStage.Idle; LastResult = result;
        CooldownRemaining = Mathf.Max(.2f, cooldownDuration);
        if (brain != null) brain.ReleaseMovementLock(this);
        if (ring != null) ring.enabled = false;
        victim = null;
    }

    void LateUpdate() { ApplyPose(); }
    public void ApplyPose()
    {
        if (!IsBusy || !body.CanWalk || foot == null || !foot.IsChildOf(transform)) return;
        float load = Mathf.SmoothStep(0, 1, elapsed / windup);
        float drop = Mathf.Clamp01((elapsed - windup) / strike);
        float recover = Mathf.Clamp01((elapsed - windup - strike) / recovery);
        float weight = 1 - Mathf.SmoothStep(0, 1, recover);
        Vector3 baseFoot = foot.position, baseSupport = supportFoot.position;
        Quaternion baseFootRotation = foot.rotation, baseSupportRotation = supportFoot.rotation;
        Vector3 toward = Vector3.ProjectOnPlane(LockedPoint - transform.position, Vector3.up).normalized;
        body.hips.position += (toward * 1.25f - Vector3.up * 1.2f) * load * weight;
        Tilt(spine, 9 * load * weight); Tilt(chest, 10 * load * weight); Tilt(neck, 8 * load * weight); Tilt(body.head, 17 * load * weight);
        // The support leg remains planted while the striking knee loads high.
        Vector3 lifted = landingFoot + Vector3.up * 2.7f - toward * .6f;
        Vector3 attackPoint = Vector3.Lerp(startFoot, lifted, load);
        if (elapsed >= windup) attackPoint = Vector3.Lerp(lifted, landingFoot, drop * drop);
        // Blend the IK TARGETS home, not the solver weight: fading a plant solver
        // while the pelvis is still lowered drives the support foot underground.
        float returnWeight = Mathf.SmoothStep(0, 1, recover);
        Vector3 supportPoint = Vector3.Lerp(Vector3.Lerp(startSupport, plantedSupport, load), baseSupport, returnWeight);
        attackPoint = Vector3.Lerp(attackPoint, baseFoot, returnWeight);
        Quaternion attackRotation = Quaternion.Slerp(Quaternion.Slerp(baseFootRotation, footFlat, load), baseFootRotation, returnWeight);
        Quaternion supportRotation = Quaternion.Slerp(Quaternion.Slerp(baseSupportRotation, supportFlat, load), baseSupportRotation, returnWeight);
        SolveLeg(supportUpper, supportLower, supportFoot, supportPoint, supportRotation, 1);
        SolveLeg(upper, lower, foot, attackPoint, attackRotation, 1);
        Brace(body.leftUpperArm, body.leftLowerArm, body.leftHand, -1, load * weight);
        Brace(body.rightUpperArm, body.rightLowerArm, body.rightHand, 1, load * weight);
    }
    void Tilt(Transform bone, float degrees) { if (bone != null && bone.IsChildOf(transform)) bone.rotation = Quaternion.AngleAxis(degrees, transform.right) * bone.rotation; }
    void Brace(Transform arm, Transform elbow, Transform hand, float side, float weight)
    {
        if (arm == null || !arm.IsChildOf(transform)) return;
        Aim(arm, elbow, arm.position + transform.right * side * 2.2f - Vector3.up * .3f + transform.forward * .45f, weight);
        // Bend in the rig's elbow hinge plane, within its existing ragdoll limits.
        // A world-space forearm target introduced excessive sideways elbow swing
        // and unstable contact configurations when this loaded pose was killed.
        Vector3 direction = hand.position - elbow.position;
        Vector3 axis = elbow.TransformDirection(Vector3.right);
        float sign = Vector3.Dot(Vector3.Cross(axis, direction), transform.forward) < 0 ? -1 : 1;
        Aim(elbow, hand, elbow.position + Quaternion.AngleAxis(sign * 28, axis) * direction, weight);
    }
    void SolveLeg(Transform hip, Transform knee, Transform ankle, Vector3 target, Quaternion rotation, float weight)
    {
        if (!hip.IsChildOf(transform)) return;
        float a = Vector3.Distance(hip.position, knee.position), b = Vector3.Distance(knee.position, ankle.position);
        Vector3 direction = target - hip.position;
        float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(a-b)+.01f, a+b-.01f);
        direction.Normalize();
        Vector3 bend = Vector3.ProjectOnPlane(transform.forward, direction).normalized;
        if (bend.sqrMagnitude < .01f) bend = transform.right;
        float along = (a*a + distance*distance - b*b) / (2*distance);
        Vector3 kneePoint = hip.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        Aim(hip, knee, kneePoint, weight); Aim(knee, ankle, target, weight);
        ankle.rotation = Quaternion.Slerp(ankle.rotation, rotation, weight);
    }
    static void Aim(Transform bone, Transform child, Vector3 target, float weight)
    {
        Vector3 from = child.position - bone.position, to = target - bone.position;
        if (from.sqrMagnitude > .0001f && to.sqrMagnitude > .0001f)
            bone.rotation = Quaternion.Slerp(bone.rotation, Quaternion.FromToRotation(from, to) * bone.rotation, weight);
    }

    void Impact()
    {
        impacted = true; ImpactCount++; LastResult = "Impact";
        Physics.SyncTransforms();
        int count = gameObject.scene.GetPhysicsScene().OverlapSphere(LockedPoint + Vector3.up * height * .5f,
            Mathf.Sqrt(radius*radius + height*height), overlaps, victimMask, QueryTriggerInteraction.Ignore);
        LastQueryOverflow = count == overlaps.Length;
        if (LastQueryOverflow) { LastResult = "Impact query saturated / no damage"; return; }
        damaged.Clear();
        for (int i = 0; i < count; i++)
        {
            if (!IsBusy || !body.CanWalk) break;
            var col = overlaps[i];
            var receiver = col.GetComponentInParent<TitanCombatTarget>();
            if (receiver == null || receiver.gameObject.scene != gameObject.scene || !receiver.isActiveAndEnabled || receiver.IsDead || damaged.Contains(receiver)) continue;
            Vector3 closest = TitanCombatTarget.ClosestCombatPoint(col, LockedPoint + Vector3.up * height * .5f);
            Vector3 offset = closest - LockedPoint;
            if (offset.y < -.35f || offset.y > height || Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude > radius*radius) continue;
            if (!ClearRay(LockedPoint + Vector3.up * .35f, col.bounds.center, receiver.transform)) continue;
            damaged.Add(receiver); receiver.TakeDamage(attackDamage, LockedPoint);
        }
    }
    bool ClearRay(Vector3 from, Vector3 to, Transform allowed)
    {
        Vector3 offset = to - from;
        if (offset.sqrMagnitude < .0001f) return true;
        int count = gameObject.scene.GetPhysicsScene().Raycast(from, offset.normalized, rays, offset.magnitude, sceneryMask, QueryTriggerInteraction.Ignore);
        if (count == rays.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Transform hit = rays[i].transform;
            if (hit.gameObject.scene != gameObject.scene) continue;
            if (hit.IsChildOf(transform) || hit == allowed || hit.IsChildOf(allowed) || hit.GetComponentInParent<TitanCombatTarget>() != null) continue;
            return false;
        }
        return true;
    }
    bool FindGround(Vector3 point, out Vector3 ground)
    {
        int count = gameObject.scene.GetPhysicsScene().Raycast(point + Vector3.up * 8, Vector3.down, rays, 16, sceneryMask, QueryTriggerInteraction.Ignore);
        ground = Vector3.zero; float best = float.NegativeInfinity;
        if (count == rays.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var hit = rays[i];
            if (hit.transform.gameObject.scene != gameObject.scene || hit.transform.IsChildOf(transform) || hit.rigidbody != null || hit.normal.y < .65f ||
                hit.point.y > transform.position.y + 1.2f || hit.transform.GetComponentInParent<TitanCombatTarget>() != null) continue;
            if (hit.point.y > best) { best = hit.point.y; ground = hit.point; }
        }
        return !float.IsNegativeInfinity(best);
    }
    void UpdateRing()
    {
        if (!drawTelegraph || !IsBusy) { if (ring != null) ring.enabled = false; return; }
        if (ring == null)
        {
            var go = new GameObject("Stomp landing telegraph"); go.transform.SetParent(transform, false);
            ring = go.AddComponent<LineRenderer>(); ring.useWorldSpace = true; ring.loop = true;
            ring.positionCount = 64; ring.widthMultiplier = .09f;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) { ringMaterial = new Material(shader); ring.sharedMaterial = ringMaterial; }
        }
        ring.enabled = true;
        Color color = Stage == TitanStompStage.Recovery ? Color.gray : Color.Lerp(Color.yellow,Color.red,Mathf.Clamp01(elapsed / windup));
        ring.startColor = ring.endColor = color;
        if (ringMaterial != null) { if (ringMaterial.HasProperty("_BaseColor")) ringMaterial.SetColor("_BaseColor",color); else ringMaterial.color = color; }
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2 / ring.positionCount;
            ring.SetPosition(i, LockedPoint + new Vector3(Mathf.Cos(angle)*radius,.06f,Mathf.Sin(angle)*radius));
        }
    }
}
