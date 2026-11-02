using UnityEngine;
using UnityEngine.AI;

public class TitanController : MonoBehaviour
{
    [Header("AI Settings")]
    public float detectionRange = 80f;
    public float grabAttackRange = 15f;
    public float stompAttackRange = 10f;
    public float directEatRange = 5f;
    public float patrolRadius = 50f;
    public float idleTimeMin = 2f;
    public float idleTimeMax = 5f;
    public float attackCooldown = 3f;

    [Header("Speed Settings")]
    public float patrolSpeed = 3.5f;
    public float chaseSpeed = 8f;
    public float crawlSpeedMultiplier = 0.5f;

    [Header("References")]
    public NavMeshAgent agent;
    public Animator animator;
    public RagdollController ragdollController;
    public TitanLimb[] limbs;
    public TitanNape nape;
    public TitanAttack titanAttack;

    [Header("Body Parts")]
    public Transform leftHand;
    public Transform rightHand;
    public Transform leftFoot;
    public Transform rightFoot;
    public Transform mouth;

    [HideInInspector] public TitanState currentState = TitanState.Idle;

    private Transform player;
    private PlayerHealth playerHealth;
    private float detectionTimer;
    private float idleTimer;
    private float attackTimer;
    private bool leftArmDestroyed;
    private bool rightArmDestroyed;
    private bool leftLegDestroyed;
    private bool rightLegDestroyed;
    private bool eyesDestroyed;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();
        if (ragdollController == null) ragdollController = GetComponent<RagdollController>();
        if (titanAttack == null) titanAttack = GetComponent<TitanAttack>();

        // Start in animated (non-ragdoll) state
        ragdollController.DisableRagdoll();

        agent.speed = patrolSpeed;
        idleTimer = Random.Range(idleTimeMin, idleTimeMax);
    }

    void Update()
    {
        if (currentState == TitanState.Dead || currentState == TitanState.Dying)
            return;

        // Throttled player detection
        detectionTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;

        switch (currentState)
        {
            case TitanState.Idle:
                UpdateIdle();
                break;
            case TitanState.Patrol:
                UpdatePatrol();
                break;
            case TitanState.Chase:
                UpdateChase();
                break;
            case TitanState.Crawling:
                UpdateCrawling();
                break;
            case TitanState.Blind:
                UpdateBlind();
                break;
            case TitanState.GrabAttack:
            case TitanState.EatAttack:
            case TitanState.StompAttack:
                // Attacks handled by TitanAttack via coroutines
                break;
        }
    }

    void UpdateIdle()
    {
        SetAnimatorState();

        if (CheckPlayerInRange(detectionRange))
        {
            TransitionTo(TitanState.Chase);
            return;
        }

        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
        {
            TransitionTo(TitanState.Patrol);
        }
    }

    void UpdatePatrol()
    {
        SetAnimatorState();

        if (CheckPlayerInRange(detectionRange))
        {
            TransitionTo(TitanState.Chase);
            return;
        }

        // Reached patrol destination
        if (!agent.pathPending && agent.remainingDistance < 1.5f)
        {
            TransitionTo(TitanState.Idle);
        }
    }

    void UpdateChase()
    {
        if (player == null) return;

        SetAnimatorState();
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // Lost player
        if (distToPlayer > detectionRange * 1.5f)
        {
            TransitionTo(TitanState.Patrol);
            return;
        }

        // Select attack
        if (attackTimer <= 0f)
        {
            if (distToPlayer <= directEatRange && IsPlayerGrounded())
            {
                // Direct eat — player is close and grounded
                StartAttack(TitanState.EatAttack);
            }
            else if (distToPlayer <= stompAttackRange)
            {
                // Stomp or grab
                if (!leftArmDestroyed || !rightArmDestroyed)
                {
                    // Randomly pick stomp or grab
                    if (Random.value > 0.5f)
                        StartAttack(TitanState.GrabAttack);
                    else
                        StartAttack(TitanState.StompAttack);
                }
                else
                {
                    StartAttack(TitanState.StompAttack);
                }
            }
            else if (distToPlayer <= grabAttackRange && (!leftArmDestroyed || !rightArmDestroyed))
            {
                StartAttack(TitanState.GrabAttack);
            }
        }
    }

    void UpdateCrawling()
    {
        if (player == null) return;

        SetAnimatorState();
        agent.speed = chaseSpeed * crawlSpeedMultiplier;

        if (CheckPlayerInRange(detectionRange))
        {
            agent.SetDestination(player.position);

            float distToPlayer = Vector3.Distance(transform.position, player.position);
            if (attackTimer <= 0f && distToPlayer <= grabAttackRange)
            {
                if (!leftArmDestroyed || !rightArmDestroyed)
                {
                    StartAttack(TitanState.GrabAttack);
                }
            }
        }
        else
        {
            agent.ResetPath();
        }
    }

    void UpdateBlind()
    {
        SetAnimatorState();
        agent.ResetPath();
        // Blind — flail in place, no pursuit
        // Could still stomp if player is very close
        if (player != null && attackTimer <= 0f)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= stompAttackRange)
            {
                StartAttack(TitanState.StompAttack);
            }
        }
    }

    void StartAttack(TitanState attackState)
    {
        attackTimer = attackCooldown;
        agent.ResetPath();
        currentState = attackState;

        switch (attackState)
        {
            case TitanState.GrabAttack:
                // Pick which hand (prefer intact arm)
                Transform hand = !leftArmDestroyed ? leftHand : rightHand;
                bool isLeft = !leftArmDestroyed;
                animator.SetTrigger(isLeft ? "grabLeft" : "grabRight");
                titanAttack.ExecuteGrab(hand, playerHealth);
                break;

            case TitanState.StompAttack:
                animator.SetTrigger("stomp");
                Transform foot = !leftLegDestroyed ? leftFoot : rightFoot;
                titanAttack.ExecuteStomp(foot);
                break;

            case TitanState.EatAttack:
                animator.SetTrigger("eat");
                titanAttack.ExecuteDirectEat(mouth, playerHealth);
                break;
        }
    }

    /// <summary>
    /// Called by TitanAttack when an attack sequence finishes.
    /// </summary>
    public void OnAttackFinished()
    {
        if (currentState == TitanState.Dead || currentState == TitanState.Dying)
            return;

        // Return to appropriate movement state
        if (eyesDestroyed)
            TransitionTo(TitanState.Blind);
        else if (leftLegDestroyed && rightLegDestroyed)
            TransitionTo(TitanState.Crawling);
        else if (leftLegDestroyed || rightLegDestroyed)
            TransitionTo(TitanState.Crawling);
        else
            TransitionTo(TitanState.Chase);
    }

    public void OnLimbDestroyed(LimbType limbType)
    {
        switch (limbType)
        {
            case LimbType.LeftArm:
                leftArmDestroyed = true;
                break;
            case LimbType.RightArm:
                rightArmDestroyed = true;
                break;
            case LimbType.LeftLeg:
                leftLegDestroyed = true;
                if (currentState != TitanState.GrabAttack && currentState != TitanState.EatAttack
                    && currentState != TitanState.StompAttack)
                {
                    TransitionTo(TitanState.Crawling);
                }
                break;
            case LimbType.RightLeg:
                rightLegDestroyed = true;
                if (currentState != TitanState.GrabAttack && currentState != TitanState.EatAttack
                    && currentState != TitanState.StompAttack)
                {
                    TransitionTo(TitanState.Crawling);
                }
                break;
            case LimbType.Eyes:
                eyesDestroyed = true;
                if (currentState != TitanState.GrabAttack && currentState != TitanState.EatAttack
                    && currentState != TitanState.StompAttack)
                {
                    TransitionTo(TitanState.Blind);
                }
                break;
        }

        // Both legs destroyed → immobile
        if (leftLegDestroyed && rightLegDestroyed)
        {
            agent.speed = 0f;
            agent.ResetPath();
        }
    }

    public void Die()
    {
        if (currentState == TitanState.Dead || currentState == TitanState.Dying)
            return;

        currentState = TitanState.Dying;

        // Release player if grabbed
        if (playerHealth != null && playerHealth.isGrabbed)
        {
            playerHealth.OnReleased();
        }

        // Disable NavMesh
        agent.enabled = false;

        // Enable ragdoll (this also disables the Animator)
        ragdollController.EnableRagdoll();

        currentState = TitanState.Dead;
    }

    void TransitionTo(TitanState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case TitanState.Idle:
                agent.ResetPath();
                idleTimer = Random.Range(idleTimeMin, idleTimeMax);
                agent.speed = 0f;
                break;

            case TitanState.Patrol:
                agent.speed = patrolSpeed;
                Vector3 randomPoint = RandomNavMeshPoint(transform.position, patrolRadius);
                agent.SetDestination(randomPoint);
                break;

            case TitanState.Chase:
                agent.speed = chaseSpeed;
                break;

            case TitanState.Crawling:
                agent.speed = chaseSpeed * crawlSpeedMultiplier;
                break;

            case TitanState.Blind:
                agent.ResetPath();
                agent.speed = 0f;
                break;
        }

        SetAnimatorState();
    }

    void SetAnimatorState()
    {
        if (animator == null) return;

        animator.SetBool("isWalking", currentState == TitanState.Patrol);
        animator.SetBool("isChasing", currentState == TitanState.Chase);
        animator.SetBool("isCrawling", currentState == TitanState.Crawling);
        animator.SetBool("isBlind", currentState == TitanState.Blind);
    }

    bool CheckPlayerInRange(float range)
    {
        if (player == null) return false;
        if (detectionTimer > 0f) return false;
        detectionTimer = 0.25f; // Throttle detection checks
        return Vector3.Distance(transform.position, player.position) <= range;
    }

    bool IsPlayerGrounded()
    {
        if (player == null) return false;
        Movement mov = player.GetComponent<Movement>();
        if (mov != null) return mov.CheckGround();
        return false;
    }

    Vector3 RandomNavMeshPoint(Vector3 origin, float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += origin;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return origin;
    }
}
