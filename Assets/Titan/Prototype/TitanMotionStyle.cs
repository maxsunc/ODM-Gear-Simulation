using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Velocity-matched gait, head-led pivots and bounded per-Titan motion variation.
/// No target lookup: heading comes only from the agent's already known path.</summary>
[DefaultExecutionOrder(20)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ModularTitanBody))]
public class TitanMotionStyle : MonoBehaviour
{
    public int styleSeed;
    public bool automaticTick = true;
    public bool previewRun;
    public bool varyPace = true;
    public float walkReferenceSpeed = 4.6f;
    public float runReferenceSpeed = 9.5f;
    public float walkTurnSpeed = 210f;
    public float runTurnSpeed = 330f;
    public float headLeadTime = .085f;
    public float PaceScale { get; private set; }
    public float CornerSpeedScale { get; private set; }
    public float Gait { get; private set; }
    public float StrideRate { get; private set; }
    public float ActualSpeed { get; private set; }
    public float HeadYaw { get; private set; }
    public float CycleOffset { get; private set; }
    public float Personality { get; private set; }

    ModularTitanBody body;
    NavMeshAgent agent;
    TitanPatrolChaseAI brain;
    Transform chest, neck;
    System.Random random;
    float paceTimer, paceTarget = 1, cadenceBias, turnBias, pivotAge, turnAngle;
    float movingWeight, runWeight, recoilAge = 10, recoilSide;
    bool turning, ownsRotation, originalUpdateRotation;
    static readonly int Walking = Animator.StringToHash("IsWalking");
    static readonly int GaitParameter = Animator.StringToHash("Gait");
    static readonly int Rate = Animator.StringToHash("StrideRate");
    static readonly int Offset = Animator.StringToHash("CycleOffset");

    void Awake()
    {
        body = GetComponent<ModularTitanBody>();
        foreach (var bone in body.hips.GetComponentsInChildren<Transform>())
        {
            if (bone.name == "Chest") chest = bone;
            if (bone.name == "Neck") neck = bone;
        }
        ResetStyle(styleSeed != 0 ? styleSeed : GetInstanceID());
    }
    void OnEnable() { body.LimbSevered += OnSevered; }
    void OnDisable()
    {
        if (body != null) body.LimbSevered -= OnSevered;
        if (ownsRotation && agent != null) agent.updateRotation = originalUpdateRotation;
        ownsRotation = false;
    }

    public void ResetStyle(int seed)
    {
        random = new System.Random(seed);
        cadenceBias = Range(.94f, 1.07f);
        turnBias = Range(.90f, 1.12f);
        Personality = Range(.80f, 1.20f);
        CycleOffset = Range(0, 1);
        PaceScale = paceTarget = CornerSpeedScale = StrideRate = 1;
        paceTimer = 0;
        if (body != null && body.animator != null) body.animator.SetFloat(Offset, CycleOffset);
    }

    void Update()
    {
        if (!automaticTick) return;
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (brain == null) brain = GetComponent<TitanPatrolChaseAI>();
        bool ready = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        if (ready && !ownsRotation)
        {
            originalUpdateRotation = agent.updateRotation;
            agent.updateRotation = false;
            ownsRotation = true;
        }
        bool chase = brain != null && brain.enabled && brain.State == TitanAwarenessState.Chase;
        float speed = ready ? Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude :
            agent == null && body.WalkingRequested ? (previewRun ? runReferenceSpeed : walkReferenceSpeed) : 0;
        turnAngle = 0;
        if (ready && agent.hasPath && !agent.isStopped)
        {
            Vector3 heading = Vector3.ProjectOnPlane(agent.steeringTarget - transform.position, Vector3.up);
            if (heading.sqrMagnitude > .01f) turnAngle = Vector3.SignedAngle(transform.forward, heading, Vector3.up);
        }
        TickMotion(Time.deltaTime, speed, chase || (agent == null && previewRun), turnAngle);
    }

    /// <summary>Explicit input form enables deterministic tests without faking agent velocity.</summary>
    public void TickMotion(float deltaTime, float speed, bool chase, float signedTurn)
    {
        if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || float.IsNaN(speed) || float.IsInfinity(speed)) return;
        if (body.IsDead) return;
        bool available = body.CanWalk;
        ActualSpeed = available ? Mathf.Max(0, speed) : 0;
        bool moving = ActualSpeed > .18f;
        if (available && moving && varyPace)
        {
            paceTimer -= deltaTime;
            if (paceTimer <= 0)
            {
                // Choose a coherent segment, not independent random noise every frame.
                bool hesitation = chase && random.NextDouble() < .18;
                paceTimer = hesitation ? Range(.12f,.22f) : Range(1.7f,3.5f);
                paceTarget = hesitation ? Range(.52f,.66f) : chase ? Range(.94f,1.20f) : Range(.90f,1.08f);
            }
        }
        else if (!varyPace || !available) paceTarget = 1;
        PaceScale = Mathf.MoveTowards(PaceScale, paceTarget, deltaTime * 2.8f);
        CornerSpeedScale = Mathf.Abs(signedTurn) > 105 ? .38f : Mathf.Abs(signedTurn) > 60 ? .68f : 1;
        float desiredGait = moving ? Mathf.InverseLerp(5.3f, 8.3f, ActualSpeed) : 0;
        Gait = Mathf.MoveTowards(Gait, desiredGait, deltaTime * 7);
        float reference = Mathf.Lerp(walkReferenceSpeed, runReferenceSpeed, Gait);
        StrideRate = moving ? Mathf.Clamp(ActualSpeed / Mathf.Max(.1f, reference) * cadenceBias, .35f, 1.65f) : 1;
        movingWeight = Mathf.MoveTowards(movingWeight, moving ? 1 : 0, deltaTime * 9);
        runWeight = Mathf.MoveTowards(runWeight, moving && chase ? 1 : 0, deltaTime * 8);
        recoilAge += deltaTime;
        if (body.animator != null && body.animator.enabled)
        {
            body.animator.SetBool(Walking, moving && available);
            body.animator.SetFloat(GaitParameter, Gait);
            body.animator.SetFloat(Rate, StrideRate);
            body.animator.SetFloat(Offset, CycleOffset);
        }
    }

    void LateUpdate()
    {
        if (!automaticTick) return;
        bool hasHeading = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.hasPath && !agent.isStopped;
        TickTurning(Time.deltaTime, turnAngle, hasHeading);
    }

    public void TickTurning(float dt, float signedTurn, bool hasHeading)
    {
        if (body.IsDead || !body.CanWalk || dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt) || float.IsNaN(signedTurn) || float.IsInfinity(signedTurn)) return;
        float angle = hasHeading ? signedTurn : 0;
        bool sharp = Mathf.Abs(angle) > 60;
        if (sharp && !turning) pivotAge = 0;
        turning = sharp;
        pivotAge += dt;
        // Head/shoulders lead the pivot briefly; root then commits quickly.
        HeadYaw = Mathf.Lerp(HeadYaw, Mathf.Clamp(angle * .72f,-58,58), 1 - Mathf.Exp(-dt * 20));
        if (hasHeading && (!sharp || pivotAge >= headLeadTime))
        {
            float maxTurn = Mathf.Lerp(walkTurnSpeed, runTurnSpeed, runWeight) * turnBias * dt;
            float step = Mathf.Clamp(angle,-maxTurn,maxTurn);
            transform.rotation = Quaternion.AngleAxis(step, Vector3.up) * transform.rotation;
        }
        if (chest != null)
        {
            float bank = Mathf.Clamp(angle * -.055f,-7,7) * movingWeight;
            chest.rotation = Quaternion.AngleAxis(bank, transform.forward) * Quaternion.AngleAxis(HeadYaw * .12f, Vector3.up) * chest.rotation;
            chest.rotation = Quaternion.AngleAxis(runWeight * 3 * (Personality - 1), transform.right) * chest.rotation;
        }
        if (neck != null) neck.rotation = Quaternion.AngleAxis(HeadYaw * .30f, Vector3.up) * neck.rotation;
        if (body.head != null) body.head.rotation = Quaternion.AngleAxis(HeadYaw * .58f, Vector3.up) * body.head.rotation;
        if (recoilAge < .27f && chest != null)
        {
            float impact = Mathf.Sin(Mathf.Min(1,recoilAge / .055f) * Mathf.PI * .5f) * Mathf.Exp(-recoilAge * 13);
            chest.rotation = Quaternion.AngleAxis(recoilSide * impact * 13, transform.forward) * chest.rotation;
        }
    }

    void OnSevered(TitanHitRegion region)
    {
        if (region == TitanHitRegion.LeftArm || region == TitanHitRegion.RightArm)
        {
            recoilAge = 0;
            recoilSide = region == TitanHitRegion.LeftArm ? -1 : 1;
        }
    }
    float Range(float min, float max) { return Mathf.Lerp(min,max,(float)random.NextDouble()); }
}
