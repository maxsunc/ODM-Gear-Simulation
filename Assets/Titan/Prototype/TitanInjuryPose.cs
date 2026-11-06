using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum TitanInjuryState { Standing, KneelingOnLeft, KneelingOnRight, Prone }

/// <summary>
/// Animated alive-but-disabled poses, not a death ragdoll. Takes skeleton ownership
/// from the Animator on leg loss, blends from the current pose, and writes only bones
/// still belonging to this body. Ground support is intended for flat/gently stepped ground.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ModularTitanBody))]
public class TitanInjuryPose : MonoBehaviour
{
    [Min(.1f)] public float kneelDuration = .58f;
    [Min(.1f)] public float proneDuration = .76f;
    [Min(0)] public float groundClearance = .06f;
    public LayerMask groundMask = ~0;
    public bool automaticTick = true;
    public TitanInjuryState State { get; private set; }
    public bool IsTransitioning { get { return State != TitanInjuryState.Standing && elapsed < duration && !body.IsDead; } }
    public float TransitionProgress { get { return duration > 0 ? Mathf.Clamp01(elapsed / duration) : 0; } }

    ModularTitanBody body;
    Transform[] bones;
    Vector3[] restPositions, fromPositions, targetPositions;
    Quaternion[] restRotations, restRootRotations, fromRotations, targetRotations;
    readonly Dictionary<string, int> indices = new Dictionary<string, int>();
    readonly RaycastHit[] groundHits = new RaycastHit[48];
    float elapsed, duration, floorY;

    void Awake()
    {
        body = GetComponent<ModularTitanBody>();
        // Cache exact imported rest transforms BEFORE the Animator's first evaluation.
        var selected = new List<Transform>();
        string[] names = {
            "Hips", "Spine", "Chest", "Neck", "Head", "Jaw",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
            "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes"
        };
        var hierarchy = body.hips.GetComponentsInChildren<Transform>(true);
        foreach (string name in names)
            foreach (var bone in hierarchy)
                if (bone.name == name) { indices.Add(name, selected.Count); selected.Add(bone); break; }
        bones = selected.ToArray();
        int count = bones.Length;
        restPositions = new Vector3[count]; fromPositions = new Vector3[count]; targetPositions = new Vector3[count];
        restRotations = new Quaternion[count]; restRootRotations = new Quaternion[count];
        fromRotations = new Quaternion[count]; targetRotations = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            restPositions[i] = bones[i].localPosition;
            restRotations[i] = bones[i].localRotation;
            restRootRotations[i] = Quaternion.Inverse(transform.rotation) * bones[i].rotation;
        }
    }

    void OnEnable() { body.LimbSevered += OnSevered; }
    void OnDisable() { if (body != null) body.LimbSevered -= OnSevered; }
    void LateUpdate() { if (automaticTick) TickPose(Time.deltaTime); }

    void OnSevered(TitanHitRegion region)
    {
        if (body.IsDead || (region != TitanHitRegion.LeftLeg && region != TitanHitRegion.RightLeg)) return;
        bool leftMissing = body.IsSevered(TitanHitRegion.LeftLeg);
        bool rightMissing = body.IsSevered(TitanHitRegion.RightLeg);
        var next = leftMissing && rightMissing ? TitanInjuryState.Prone :
            leftMissing ? TitanInjuryState.KneelingOnRight : TitanInjuryState.KneelingOnLeft;
        if (next == State) return;
        BeginTransition(next);
    }

    void BeginTransition(TitanInjuryState next)
    {
        // Capture BEFORE building the target. A second cut continues from the current
        // partly collapsed pose, never from a standing reset. Detached bones are excluded.
        for (int i = 0; i < bones.Length; i++)
            if (Attached(i)) { fromPositions[i] = bones[i].localPosition; fromRotations[i] = bones[i].localRotation; }
        if (body.animator != null) body.animator.enabled = false;
        State = next;
        elapsed = 0;
        duration = next == TitanInjuryState.Prone ? proneDuration : kneelDuration;
        floorY = FindFloor();
        try
        {
            for (int i = 0; i < bones.Length; i++)
                if (Attached(i)) { bones[i].localPosition = restPositions[i]; bones[i].localRotation = restRotations[i]; }
            if (next == TitanInjuryState.Prone) BuildProne(); else BuildKneel(next == TitanInjuryState.KneelingOnLeft ? -1 : 1);
            // Settle the target on the ground using actual intact solid collider shapes.
            float lowest = LowestIntactCollider();
            if (!float.IsInfinity(lowest)) body.hips.position += Vector3.up * (floorY + groundClearance - lowest);
            for (int i = 0; i < bones.Length; i++)
                if (Attached(i)) { targetPositions[i] = bones[i].localPosition; targetRotations[i] = bones[i].localRotation; }
        }
        finally
        {
            for (int i = 0; i < bones.Length; i++)
                if (Attached(i)) { bones[i].localPosition = fromPositions[i]; bones[i].localRotation = fromRotations[i]; }
        }
    }

    public void TickPose(float deltaTime)
    {
        if (body.IsDead || State == TitanInjuryState.Standing || deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        elapsed = Mathf.Min(duration, elapsed + deltaTime);
        float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, duration));
        for (int i = 0; i < bones.Length; i++)
            if (Attached(i))
            {
                float boneTime = t;
                if (bones[i].name.EndsWith("Arm") || bones[i].name.EndsWith("Hand")) boneTime = Mathf.Min(1,t*1.18f);
                else if (bones[i] == body.head) boneTime = Mathf.Clamp01(t*1.035f-.035f);
                float eased = FallWeight(boneTime);
                bones[i].localPosition = Vector3.LerpUnclamped(fromPositions[i], targetPositions[i], eased);
                bones[i].localRotation = Quaternion.SlerpUnclamped(fromRotations[i], targetRotations[i], eased);
            }
        // Folding the surviving shin traces an arc. Lift the pelvis as necessary
        // during the fall rather than letting the foot pass through the ground.
        float lowest = LowestIntactCollider();
        if (lowest < floorY + groundClearance) body.hips.position += Vector3.up * (floorY + groundClearance - lowest);
    }

    // Brief buckle, fast gravity-like drop, impact and damped recovery. Not the
    // uniform ease-in/ease-out that made the original collapse feel weightless.
    public static float FallWeight(float time)
    {
        float t = Mathf.Clamp01(time);
        if (t <= .10f) return .04f * Mathf.SmoothStep(0,1,t/.10f);
        if (t < .62f) return .04f + .96f * Mathf.Pow((t-.10f)/.52f,.72f);
        float settle = (t-.62f)/.38f;
        return 1 + .035f * Mathf.Sin(settle*Mathf.PI*2) * Mathf.Exp(-settle*3);
    }

    void BuildKneel(float side)
    {
        string leg = side < 0 ? "Left" : "Right";
        float floorLocal = transform.InverseTransformPoint(new Vector3(transform.position.x, floorY, transform.position.z)).y;
        body.hips.position = Point(side * .35f, floorLocal + 3.12f, -.22f);
        body.hips.rotation = transform.rotation * Quaternion.Euler(7, 0, -side * 8) * RestRoot("Hips");
        Tilt("Spine", 8); Tilt("Chest", 7); Tilt("Neck", 8); Tilt("Head", 20);
        var upper = Bone(leg + "UpperLeg"); var lower = Bone(leg + "LowerLeg"); var foot = Bone(leg + "Foot");
        Aim(upper, lower, Point(side * .98f, floorLocal + .62f, .30f));
        Aim(lower, foot, lower.position - transform.forward * 2.4f + Vector3.up * .06f);
        foot.rotation = transform.rotation * Quaternion.Euler(0,180,0) * RestRoot(leg + "Foot");
        PoseArm("Left", -1, floorLocal, false);
        PoseArm("Right", 1, floorLocal, false);
    }

    void BuildProne()
    {
        float floorLocal = transform.InverseTransformPoint(new Vector3(transform.position.x, floorY, transform.position.z)).y;
        body.hips.position = Point(0, floorLocal + 1.02f, -.80f);
        // A low forward collapse, not a flat neck buried between the shoulders.
        // Raise the upper back while bowing the head to expose the REAL nape.
        // Keep the same root, skeleton lengths, damage zone and helpless state.
        body.hips.rotation = transform.rotation * Quaternion.Euler(78, 0, 0) * RestRoot("Hips");
        Tilt("Spine", -6); Tilt("Chest", -4); Tilt("Neck", 6); Tilt("Head", 26);
        PoseArm("Left", -1, floorLocal, true);
        PoseArm("Right", 1, floorLocal, true);
    }

    void PoseArm(string side, float sign, float floorLocal, bool prone)
    {
        var region = sign < 0 ? TitanHitRegion.LeftArm : TitanHitRegion.RightArm;
        if (body.IsSevered(region)) return;
        var upper = Bone(side + "UpperArm"); var lower = Bone(side + "LowerArm"); var hand = Bone(side + "Hand");
        Vector3 elbow = prone ? Point(sign * 3.1f, floorLocal + 1.0f, 2.6f) : Point(sign * 2.2f, floorLocal + 4.4f, 1.1f);
        Vector3 palm = prone ? Point(sign * 3.6f, floorLocal + .60f, 4.2f) : Point(sign * 1.3f, floorLocal + 2.9f, .7f);
        Aim(upper, lower, elbow);
        Aim(lower, hand, palm);
    }

    void Tilt(string name, float angle)
    {
        var bone = Bone(name);
        bone.rotation = Quaternion.AngleAxis(angle, transform.right) * bone.rotation;
    }
    static void Aim(Transform bone, Transform child, Vector3 target)
    {
        Vector3 direction = child.position - bone.position;
        if (direction.sqrMagnitude > .0001f && (target - bone.position).sqrMagnitude > .0001f)
            bone.rotation = Quaternion.FromToRotation(direction, target - bone.position) * bone.rotation;
    }
    Vector3 Point(float x, float y, float z) { return transform.TransformPoint(new Vector3(x,y,z)); }
    Transform Bone(string name) { return bones[indices[name]]; }
    Quaternion RestRoot(string name) { return restRootRotations[indices[name]]; }
    bool Attached(int index) { return bones[index] != null && bones[index].IsChildOf(transform); }

    float FindFloor()
    {
        // Ignore dynamic debris, self, damage triggers and ceilings; never perch on
        // the leg that was just severed. Keep the ray in this body's physics scene.
        int count = gameObject.scene.GetPhysicsScene().Raycast(transform.position + Vector3.up * 20,
            Vector3.down, groundHits, 50, groundMask, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.transform.IsChildOf(transform) || hit.rigidbody != null || hit.normal.y < .5f || hit.point.y > transform.position.y + 1.5f) continue;
            best = Mathf.Max(best, hit.point.y);
        }
        return float.IsNegativeInfinity(best) ? transform.position.y : best;
    }

    float LowestIntactCollider()
    {
        // Compute from transforms, not Collider.bounds: the latter may still contain
        // the previous physics tick while building/blending an animated pose.
        float lowest = float.PositiveInfinity;
        foreach (var entry in body.ragdollBones)
        {
            var collider = entry.collider;
            if (collider == null || !collider.enabled || !collider.transform.IsChildOf(transform)) continue;
            var box = collider as BoxCollider;
            if (box != null)
            {
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            lowest = Mathf.Min(lowest, box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, new Vector3(x,y,z))).y);
                continue;
            }
            var capsule = collider as CapsuleCollider;
            if (capsule != null)
            {
                Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                Vector3 scaled = capsule.transform.TransformVector(axis);
                var scale = capsule.transform.lossyScale;
                float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                float segment = Mathf.Max(0, capsule.height * scaled.magnitude * .5f - radius);
                lowest = Mathf.Min(lowest, capsule.transform.TransformPoint(capsule.center).y - Mathf.Abs(scaled.normalized.y) * segment - radius);
            }
        }
        return lowest;
    }
}
