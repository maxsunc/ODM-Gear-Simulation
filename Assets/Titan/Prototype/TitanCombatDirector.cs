using UnityEngine;
using UnityEngine.AI;

/// <summary>Attack arbitration: one attack at a time, coherent choice, fixed telegraphs.
/// Uses local RNG, never gameplay's UnityEngine.Random. No attack components on body prefab.</summary>
[DefaultExecutionOrder(40)]
[DisallowMultipleComponent]
[RequireComponent(typeof(TitanGrabAttack), typeof(TitanStompAttack))]
public class TitanCombatDirector : MonoBehaviour
{
    public bool automaticTick = true;
    public bool automaticAttack = true;
    public int decisionSeed;
    [Range(0,1)] public float groundStompChance=.45f;
    [Header("Ground attack reaction")]
    [Tooltip("Seconds fully stopped before choosing a ground attack. Does not shorten its wind-up.")]
    [Min(0)] public float groundSettleDuration=.06f;
    [Tooltip("Seconds before trying again when neither ground attack can start.")]
    [Min(0)] public float failedAttackRetryDelay=.05f;
    [Tooltip("Extra decision pause after an attack finishes recovery. Individual attack cooldowns still apply.")]
    [Min(0)] public float postAttackDecisionDelay=.2f;
    [Header("Runtime status")]
    [SerializeField, Tooltip("Diagnostic status only; changing this text does not affect AI.")]
    string waitReason="Not evaluated";
    public string WaitReason { get { return waitReason; } }
    public string LastChoice { get; private set; }
    TitanGrabAttack grab;
    TitanStompAttack stomp;
    TitanPatrolChaseAI brain;
    NavMeshAgent agent;
    System.Random random;
    float stationaryAge, decisionDelay;
    bool wasBusy;
    void Awake()
    {
        grab = GetComponent<TitanGrabAttack>(); stomp = GetComponent<TitanStompAttack>();
        brain = GetComponent<TitanPatrolChaseAI>(); agent = GetComponent<NavMeshAgent>();
        stomp.automaticAttack = false;
        ResetDecisions(decisionSeed);
    }
    public void ResetDecisions(int seed) { random = new System.Random(seed == 0 ? GetInstanceID() : seed); }
    void Update() { if (automaticTick) TickSelection(Time.deltaTime); }
    void OnDisable() { CancelAttacks("Combat director disabled"); }
    public void SetAutomaticAttacks(bool enabled)
    {
        automaticAttack = enabled;
        if (!enabled) CancelAttacks("Auto attacks paused");
    }
    public void CancelAttacks(string reason) { if (grab != null) grab.Cancel(reason); if (stomp != null) stomp.Cancel(reason); }
    public void TickSelection(float deltaTime)
    {
        if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        bool busy = grab.IsBusy || stomp.IsBusy;
        if (wasBusy && !busy) decisionDelay = SafeDelay(postAttackDecisionDelay,.2f);
        wasBusy = busy;
        decisionDelay = Mathf.Max(0, decisionDelay - deltaTime);
        bool settled = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.isStopped && agent.velocity.sqrMagnitude < .25f;
        stationaryAge = settled ? stationaryAge + deltaTime : 0;
        if (!isActiveAndEnabled || !automaticAttack) { waitReason="Automatic attacks disabled"; return; }
        if (busy) { waitReason="Attack in progress or recovering"; return; }
        if (brain == null || !brain.isActiveAndEnabled || brain.State != TitanAwarenessState.Chase || !brain.CanSeeTarget || brain.target == null)
        { waitReason="Waiting for a visible chase target"; return; }
        var victim = brain.target.GetComponent<TitanCombatTarget>();
        if (victim == null || victim.IsDead || victim.IsHeld || victim.ReleasePending)
        { waitReason="Target unavailable, dead, held or awaiting release"; return; }
        bool leftFoot = transform.InverseTransformPoint(victim.transform.position).x < 0;
        bool leftHand = leftFoot;
        var body = GetComponent<ModularTitanBody>();
        if (!(leftHand ? body.CanGrabLeft : body.CanGrabRight)) leftHand = !leftHand;
        if(grab.TryBeginAerialGrab(victim,leftHand) || grab.TryBeginAerialGrab(victim,!leftHand))
        {
            LastChoice="Aerial grab"; waitReason="Aerial grab started";
            decisionDelay=SafeDelay(postAttackDecisionDelay,.2f); wasBusy=true; return;
        }
        // Do not lose the brief aerial reach window to a ground-choice retry
        // delay or stationary-age gate. Grab's own cooldown still rate-limits it.
        if(decisionDelay>0) { waitReason="Decision retry or post-attack pause"; return; }
        // Even a zero settling time must not bypass the existing braking gate.
        if(!settled) { waitReason="Waiting for navigation to stop / finish braking"; return; }
        if(stationaryAge<SafeDelay(groundSettleDuration,.06f))
        { waitReason="Planting feet before a ground attack"; return; }
        bool preferGrab = random.NextDouble() < 1-Mathf.Clamp01(groundStompChance);
        bool started;
        if (preferGrab)
        {
            started = grab.TryBeginGrab(victim, leftHand);
            if (!started) started = stomp.TryBeginStomp(victim, leftFoot);
        }
        else
        {
            started = stomp.TryBeginStomp(victim, leftFoot);
            if (!started) started = grab.TryBeginGrab(victim, leftHand);
        }
        LastChoice = grab.IsBusy ? "Grab" : stomp.IsBusy ? "Stomp" : "No valid attack";
        waitReason = started ? "Ground attack started" : stomp.CooldownRemaining>0
            ? "Stomp cooldown active; grab also unavailable"
            : "No valid ground attack: check range, facing, sight, ground, cover or injury";
        decisionDelay = started ? SafeDelay(postAttackDecisionDelay,.2f) : SafeDelay(failedAttackRetryDelay,.05f);
        wasBusy = started;
    }
    static float SafeDelay(float value,float fallback)
    { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0,value); }
}
