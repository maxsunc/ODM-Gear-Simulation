using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public enum TitanAwarenessState { Patrol, Chase, Investigate, Search, Blind, Immobilized, Dead, NavigationUnavailable }

/// <summary>
/// Opt-in navigation/perception layer for the modular body. No attacks or player dependencies.
/// Assign a target ROOT explicitly; losing sight never reads its new position for navigation.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ModularTitanBody), typeof(NavMeshAgent))]
public class TitanPatrolChaseAI : MonoBehaviour
{
    public Transform target;
    public Vector3 targetSightOffset = Vector3.up;
    public Vector3[] patrolPoints = new Vector3[0];
    public bool automaticTick = true;
    [Header("Sight")]
    [Min(1)] public float sightRange = 258f;
    [Range(1, 360)] public float fieldOfView = 300f;
    public LayerMask sightMask = ~0;
    [Min(.02f)] public float perceptionInterval = .15f;
    [Min(0)] public float targetMemoryDuration = 14f;
    [Header("Navigation")]
    [Min(.1f)] public float patrolSpeed = 8f;
    [Min(.1f)] public float chaseSpeed = 20.5f;
    [Min(0)] public float chaseStoppingDistance = 6f;
    [Min(.1f)] public float arrivalDistance = .8f;
    [Min(0)] public float patrolPause = 1.5f;
    [Min(.02f)] public float repathInterval = .3f;
    [Min(.1f)] public float projectionDistance = 3f;
    [Header("Stationary search look")]
    [Min(0)] public float searchDuration = 4f;
    [Tooltip("Peak degrees per second of the side-to-side upper-body look. Does not rotate the root or feet.")]
    [Min(0)] public float searchTurnSpeed = 75f;
    [Tooltip("Maximum look angle to either side of straight ahead.")]
    [Range(0, 60)] public float searchLookAngle = 60f;

    public TitanAwarenessState State { get; private set; }
    public bool CanSeeTarget { get; private set; }
    public bool HasLastKnownPosition { get; private set; }
    public Vector3 LastKnownPosition { get; private set; }
    public string NavigationStatus { get; private set; }
    public int PatrolIndex { get { return patrolIndex; } }
    public float SearchLookYaw { get; private set; }

    ModularTitanBody body;
    NavMeshAgent agent;
    TitanMotionStyle motion;
    Transform eyes, observedTarget;
    MonoBehaviour movementOwner;
    public bool IsMovementLocked { get { return movementOwner != null && movementOwner.isActiveAndEnabled; } }
    public bool TryAcquireMovementLock(MonoBehaviour owner)
    {
        if (owner == null || !owner.isActiveAndEnabled || (IsMovementLocked && movementOwner != owner)) return false;
        movementOwner = owner;
        StopMovement();
        return true;
    }
    public void ReleaseMovementLock(MonoBehaviour owner)
    {
        if (movementOwner != owner) return;
        movementOwner = null;
        pathTimer = 0;
    }
    readonly RaycastHit[] sightHits = new RaycastHit[64];
    NavMeshPath path;
    float senseTimer, pathTimer, memoryAge, waitTimer, searchLookTravel;
    int patrolIndex;
    bool patrolWaiting, subscribed, hasRequestedPoint;
    Vector3 requestedPoint;
    MoveResult lastMoveResult;
    enum MoveResult { Moving, Arrived, Blocked }

    void Awake()
    {
        body = GetComponent<ModularTitanBody>();
        agent = GetComponent<NavMeshAgent>();
        motion = GetComponent<TitanMotionStyle>();
        path = new NavMeshPath();
        foreach (var zone in body.hitZones)
            if (zone != null && zone.region == TitanHitRegion.Eyes) { eyes = zone.transform; break; }
        State = TitanAwarenessState.Patrol;
        NavigationStatus = "Ready";
    }

    void OnEnable()
    {
        body.BlindnessChanged += OnBlindness;
        body.LimbSevered += OnSevered;
        body.Died += OnDied;
        subscribed = true;
        senseTimer = pathTimer = 0;
        ChooseNearestPatrolPoint();
    }

    void OnDisable()
    {
        if (subscribed)
        {
            body.BlindnessChanged -= OnBlindness;
            body.LimbSevered -= OnSevered;
            body.Died -= OnDied;
            subscribed = false;
        }
        SearchLookYaw = searchLookTravel = 0;
        StopMovement();
    }

    void Update() { if (automaticTick) TickAI(Time.deltaTime); }

    public void TickAI(float deltaTime)
    {
        if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        if (body.IsDead) { OnDied(); return; }
        if (HasLastKnownPosition) memoryAge += deltaTime;
        senseTimer -= deltaTime;
        pathTimer -= deltaTime;
        if (body.IsBlind) { OnBlindness(true); return; }
        if (!body.CanWalk) { SetStoppedState(TitanAwarenessState.Immobilized); return; }
        if (!AgentReady())
        {
            SetStoppedState(TitanAwarenessState.NavigationUnavailable);
            NavigationStatus = "Agent disabled or not on a NavMesh";
            return;
        }
        if (target != observedTarget)
        {
            // A replacement target must earn detection independently of the previous one.
            if (target != null) HasLastKnownPosition = false;
            observedTarget = target;
            CanSeeTarget = false;
            senseTimer = 0;
        }
        if (senseTimer <= 0)
        {
            senseTimer = Mathf.Max(.02f, perceptionInterval);
            CanSeeTarget = HasLineOfSight();
            if (CanSeeTarget)
            {
                LastKnownPosition = target.position;
                HasLastKnownPosition = true;
                memoryAge = 0;
            }
        }
        // Never keep a stale positive sight result after a target is destroyed/disabled.
        if (target == null || !target.gameObject.activeInHierarchy) CanSeeTarget = false;
        // Keep perception/memory alive while an attack owns movement, but never
        // let a chase repath restart navigation during its wind-up or recovery.
        if (IsMovementLocked) { StopMovement(); return; }
        if (CanSeeTarget)
        {
            EnterState(TitanAwarenessState.Chase);
            MoveTo(LastKnownPosition, chaseStoppingDistance, chaseSpeed);
            return;
        }
        if (HasLastKnownPosition && memoryAge <= targetMemoryDuration)
        {
            if (State == TitanAwarenessState.Search)
            {
                StopMovement();
                // Pose data only: TitanMotionStyle owns the torso/head overlay.
                // One right-left-center sweep, then rest. MotionStyle eases
                // the corners; travel is bounded by searchTurnSpeed in deg/s.
                float amplitude = Mathf.Clamp(searchLookAngle, 0, 60);
                if (amplitude > .001f && !float.IsNaN(searchTurnSpeed) && !float.IsInfinity(searchTurnSpeed))
                {
                    searchLookTravel = Mathf.Min(amplitude * 4, searchLookTravel + Mathf.Max(0, searchTurnSpeed) * deltaTime);
                    SearchLookYaw = searchLookTravel < amplitude ? searchLookTravel : searchLookTravel < amplitude * 3 ? amplitude * 2 - searchLookTravel : searchLookTravel - amplitude * 4;
                }
                else SearchLookYaw = 0;
                waitTimer -= deltaTime;
                if (waitTimer <= 0) ReturnToPatrol();
            }
            else
            {
                EnterState(TitanAwarenessState.Investigate);
                var result = MoveTo(LastKnownPosition, .3f, patrolSpeed);
                if (result != MoveResult.Moving)
                {
                    EnterState(TitanAwarenessState.Search);
                    waitTimer = searchDuration;
                    StopMovement();
                }
            }
            return;
        }
        if (State != TitanAwarenessState.Patrol) ReturnToPatrol();
        TickPatrol(deltaTime);
    }

    bool HasLineOfSight()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        Vector3 origin = eyes != null ? eyes.position : body.head.position;
        Vector3 offset = target.TransformPoint(targetSightOffset) - origin;
        float distance = offset.magnitude;
        if (distance > sightRange || distance < .001f) return false;
        Vector3 horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
        if (horizontal.sqrMagnitude > .001f && Vector3.Angle(transform.forward, horizontal) > fieldOfView * .5f) return false;
        // Scene-local raycast also works in the disposable local-physics test scenes.
        int count = gameObject.scene.GetPhysicsScene().Raycast(origin, offset / distance,
            sightHits, distance, sightMask, QueryTriggerInteraction.Ignore);
        if (count == sightHits.Length) return false; // Fail closed if the buffer overflows.
        for (int i = 0; i < count; i++)
        {
            var hit = sightHits[i].transform;
            if (hit == transform || hit.IsChildOf(transform) || hit == target || hit.IsChildOf(target)) continue;
            return false;
        }
        return true;
    }

    void TickPatrol(float deltaTime)
    {
        if (patrolPoints == null || patrolPoints.Length == 0) { NavigationStatus = "No patrol points"; StopMovement(); return; }
        patrolIndex %= patrolPoints.Length;
        if (patrolWaiting)
        {
            StopMovement();
            waitTimer -= deltaTime;
            if (waitTimer <= 0) { patrolIndex = (patrolIndex + 1) % patrolPoints.Length; patrolWaiting = false; pathTimer = 0; }
            return;
        }
        var result = MoveTo(patrolPoints[patrolIndex], .3f, patrolSpeed);
        if (result != MoveResult.Moving)
        {
            patrolWaiting = true;
            waitTimer = result == MoveResult.Arrived ? patrolPause : .5f;
        }
    }

    MoveResult MoveTo(Vector3 worldPoint, float stopDistance, float speed)
    {
        float styleScale = motion != null && motion.isActiveAndEnabled ? motion.PaceScale * motion.CornerSpeedScale : 1;
        agent.speed = Mathf.Max(.1f, speed * styleScale);
        agent.stoppingDistance = Mathf.Max(0, stopDistance);
        // Retry unreachable/arrived goals at the repath rate, not every rendered frame.
        if (!agent.hasPath && pathTimer > 0 && hasRequestedPoint &&
            (requestedPoint - worldPoint).sqrMagnitude < .25f && lastMoveResult != MoveResult.Moving)
            return lastMoveResult;
        if (pathTimer <= 0 || !agent.hasPath)
        {
            pathTimer = Mathf.Max(.02f, repathInterval);
            requestedPoint = worldPoint;
            hasRequestedPoint = true;
            NavMeshHit sample;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            bool found = NavMesh.SamplePosition(worldPoint, out sample, projectionDistance, filter);
            // ODM targets may be airborne; chase the reachable ground beneath them.
            if (!found)
                found = NavMesh.SamplePosition(new Vector3(worldPoint.x, transform.position.y, worldPoint.z), out sample, projectionDistance, filter);
            if (!found || !agent.CalculatePath(sample.position, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                NavigationStatus = "Destination unreachable";
                StopMovement();
                return lastMoveResult = MoveResult.Blocked;
            }
            if (Vector3.Distance(transform.position, sample.position) <= Mathf.Max(arrivalDistance, stopDistance))
            {
                NavigationStatus = "At destination";
                StopMovement();
                return lastMoveResult = MoveResult.Arrived;
            }
            agent.isStopped = false;
            if (!agent.SetPath(path))
            {
                NavigationStatus = "Path rejected";
                StopMovement();
                return lastMoveResult = MoveResult.Blocked;
            }
            NavigationStatus = "Following complete path";
        }
        if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(arrivalDistance, stopDistance))
        {
            NavigationStatus = "At destination";
            StopMovement();
            return lastMoveResult = MoveResult.Arrived;
        }
        agent.isStopped = false;
        body.SetWalking(agent.velocity.sqrMagnitude > .04f || agent.desiredVelocity.sqrMagnitude > .04f);
        return lastMoveResult = MoveResult.Moving;
    }

    void ReturnToPatrol()
    {
        HasLastKnownPosition = false;
        EnterState(TitanAwarenessState.Patrol);
        ChooseNearestPatrolPoint();
        StopMovement();
    }

    void ChooseNearestPatrolPoint()
    {
        patrolIndex = 0;
        float best = float.PositiveInfinity;
        if (patrolPoints != null)
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                float distance = (patrolPoints[i] - transform.position).sqrMagnitude;
                if (distance < best) { best = distance; patrolIndex = i; }
            }
        patrolWaiting = false;
    }

    void EnterState(TitanAwarenessState next)
    {
        if (State == next) return;
        if (State == TitanAwarenessState.Search || next == TitanAwarenessState.Search) SearchLookYaw = searchLookTravel = 0;
        State = next;
        pathTimer = 0;
    }

    bool AgentReady() { return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh; }
    void StopMovement()
    {
        // Unity 2021 ResetPath clears the stopped flag: set it AFTER clearing the path.
        if (AgentReady()) { agent.ResetPath(); agent.isStopped = true; }
        if (body != null) body.SetWalking(false);
    }
    void SetStoppedState(TitanAwarenessState state)
    {
        EnterState(state);
        CanSeeTarget = false;
        senseTimer = 0;
        StopMovement();
    }
    void OnBlindness(bool blind)
    {
        if (blind && !body.IsDead) SetStoppedState(TitanAwarenessState.Blind);
        else senseTimer = 0;
    }
    void OnSevered(TitanHitRegion region)
    {
        if (!body.IsDead && !body.CanWalk) SetStoppedState(body.IsBlind ? TitanAwarenessState.Blind : TitanAwarenessState.Immobilized);
    }
    void OnDied() { SetStoppedState(TitanAwarenessState.Dead); }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (patrolPoints != null && patrolPoints.Length > 0)
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Gizmos.DrawWireSphere(patrolPoints[i], .6f);
                Gizmos.DrawLine(patrolPoints[i], patrolPoints[(i + 1) % patrolPoints.Length]);
            }
        if (HasLastKnownPosition) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(LastKnownPosition, 1); }
    }
}
