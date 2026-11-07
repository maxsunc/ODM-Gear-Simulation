using System.Collections;
using UnityEngine;

public class TitanAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float grabDamage = 20f;
    public float stompDamage = 40f;
    public float stompRadius = 8f;
    public float eatDelay = 2f;
    public float grabEscapeTime = 3f;
    public int grabEscapePresses = 15;
    public LayerMask playerLayer;

    [Header("References")]
    public TitanController controller;

    private Coroutine currentAttack;

    void Start()
    {
        if (controller == null) controller = GetComponent<TitanController>();
    }

    public void ExecuteGrab(Transform hand, PlayerHealth playerHealth)
    {
        if (currentAttack != null) StopCoroutine(currentAttack);
        currentAttack = StartCoroutine(GrabSequence(hand, playerHealth));
    }

    public void ExecuteStomp(Transform foot)
    {
        if (currentAttack != null) StopCoroutine(currentAttack);
        currentAttack = StartCoroutine(StompSequence(foot));
    }

    public void ExecuteDirectEat(Transform mouth, PlayerHealth playerHealth)
    {
        if (currentAttack != null) StopCoroutine(currentAttack);
        currentAttack = StartCoroutine(DirectEatSequence(mouth, playerHealth));
    }

    IEnumerator GrabSequence(Transform hand, PlayerHealth playerHealth)
    {
        // Wait for grab animation to reach contact point
        yield return new WaitForSeconds(0.5f);

        // Check if player is in grab range
        if (playerHealth == null || playerHealth.isDead)
        {
            controller.OnAttackFinished();
            yield break;
        }

        float distToPlayer = Vector3.Distance(hand.position, playerHealth.transform.position);
        if (distToPlayer > controller.grabAttackRange)
        {
            // Grab missed
            yield return new WaitForSeconds(0.5f);
            controller.OnAttackFinished();
            yield break;
        }

        // Grab succeeded — attach player to hand
        playerHealth.OnGrabbed(hand);
        playerHealth.TakeDamage(grabDamage);

        // Escape window — player mashes F to escape
        float escapeTimer = 0f;
        int escapeCount = 0;
        bool escaped = false;

        while (escapeTimer < grabEscapeTime)
        {
            escapeTimer += Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.F))
            {
                escapeCount++;
                if (escapeCount >= grabEscapePresses)
                {
                    escaped = true;
                    break;
                }
            }
            yield return null;
        }

        if (escaped)
        {
            // Player escaped
            playerHealth.OnReleased();
            controller.OnAttackFinished();
            yield break;
        }

        // Player didn't escape — proceed to eat
        yield return StartCoroutine(EatSequence(playerHealth));
    }

    IEnumerator EatSequence(PlayerHealth playerHealth)
    {
        if (playerHealth == null || playerHealth.isDead)
        {
            controller.OnAttackFinished();
            yield break;
        }

        controller.currentState = TitanState.EatAttack;

        if (controller.animator != null)
        {
            controller.animator.SetTrigger("eat");
        }

        // Move player towards mouth over eatDelay seconds
        Transform mouth = controller.mouth;
        float elapsed = 0f;
        Vector3 startPos = playerHealth.transform.localPosition;

        while (elapsed < eatDelay)
        {
            if (playerHealth.isDead) break;

            elapsed += Time.deltaTime;
            float t = elapsed / eatDelay;

            if (playerHealth.transform.parent != null)
            {
                // Move within parent (hand) space towards mouth world position
                playerHealth.transform.position = Vector3.Lerp(
                    playerHealth.transform.position,
                    mouth.position,
                    t
                );
            }
            yield return null;
        }

        // Instant kill
        if (!playerHealth.isDead)
        {
            playerHealth.TakeDamage(playerHealth.maxHP * 10f);
        }

        controller.OnAttackFinished();
    }

    IEnumerator StompSequence(Transform foot)
    {
        // Wait for stomp animation to reach impact frame
        yield return new WaitForSeconds(0.6f);

        // AoE damage at foot position
        Collider[] hits = Physics.OverlapSphere(foot.position, stompRadius, playerLayer);
        foreach (Collider hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph == null) ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                ph.TakeDamage(stompDamage);
            }
        }

        // Recovery time
        yield return new WaitForSeconds(0.5f);

        controller.OnAttackFinished();
    }

    IEnumerator DirectEatSequence(Transform mouth, PlayerHealth playerHealth)
    {
        // Quick snap — wait for animation
        yield return new WaitForSeconds(0.4f);

        if (playerHealth == null || playerHealth.isDead)
        {
            controller.OnAttackFinished();
            yield break;
        }

        float distToPlayer = Vector3.Distance(mouth.position, playerHealth.transform.position);
        if (distToPlayer > controller.directEatRange)
        {
            // Missed
            yield return new WaitForSeconds(0.3f);
            controller.OnAttackFinished();
            yield break;
        }

        // Instant kill
        playerHealth.TakeDamage(playerHealth.maxHP * 10f);

        yield return new WaitForSeconds(0.5f);
        controller.OnAttackFinished();
    }
}
