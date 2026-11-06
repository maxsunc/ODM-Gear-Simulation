using UnityEngine;
using UnityEngine.AI;

/// <summary>Sandbox-only arbitration: one attack at a time, coherent choice, fixed telegraphs.
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
        if (wasBusy && !busy) decisionDelay = .65f;
        wasBusy = busy;
        decisionDelay = Mathf.Max(0, decisionDelay - deltaTime);
        bool settled = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.isStopped && agent.velocity.sqrMagnitude < .25f;
        stationaryAge = settled ? stationaryAge + deltaTime : 0;
        if (!isActiveAndEnabled || !automaticAttack || busy ||
            brain == null || !brain.isActiveAndEnabled || brain.State != TitanAwarenessState.Chase || !brain.CanSeeTarget || brain.target == null) return;
        var victim = brain.target.GetComponent<TitanCombatTarget>();
        if (victim == null || victim.IsDead || victim.IsHeld || victim.ReleasePending) return;
        bool leftFoot = transform.InverseTransformPoint(victim.transform.position).x < 0;
        bool leftHand = leftFoot;
        var body = GetComponent<ModularTitanBody>();
        if (!(leftHand ? body.CanGrabLeft : body.CanGrabRight)) leftHand = !leftHand;
        if(grab.TryBeginAerialGrab(victim,leftHand) || grab.TryBeginAerialGrab(victim,!leftHand))
        {
            LastChoice="Aerial grab"; decisionDelay=.65f; wasBusy=true; return;
        }
        // Do not lose the brief aerial reach window to a ground-choice retry
        // delay or stationary-age gate. Grab's own cooldown still rate-limits it.
        if(decisionDelay>0 || stationaryAge<.12f) return;
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
        decisionDelay = started ? .65f : .2f;
        wasBusy = started;
    }
}
