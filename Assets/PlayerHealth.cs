using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHP = 100f;
    public float currentHP;

    [Header("References")]
    public RagdollController ragdollController;

    [HideInInspector] public bool isDead = false;
    [HideInInspector] public bool isGrabbed = false;
    [HideInInspector] public Transform grabbedByTitan;

    // Integration listens before ragdoll ownership takes over; it does not use
    // legacy OnGrabbed's skeleton parenting path.
    public event System.Action Died;
    private Movement movement;
    private ODM_Movement odmMovement;
    private Attack attack;

    void Start()
    {
        currentHP = maxHP;
        movement = GetComponent<Movement>();
        odmMovement = GetComponent<ODM_Movement>();
        attack = GetComponent<Attack>();
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHP -= damage;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        if(Died!=null) Died();

        // No live gravity/jump/rope coroutine may keep moving a dead controller.
        if (movement != null) { movement.StopAllCoroutines();movement.enabledMovement = false;movement.enabled=false; }
        if (odmMovement != null) { odmMovement.SuspendForTitan(this);odmMovement.enabled = false; }
        if (attack != null) attack.enabled = false;

        // Enable ragdoll on player
        if (ragdollController != null)
        {
            ragdollController.EnableRagdoll();
        }

        // Reload scene after a short delay (matches GameElements.cs pattern)
        Invoke(nameof(ReloadScene), 3f);
    }

    public void OnGrabbed(Transform hand)
    {
        if (isDead) return;

        isGrabbed = true;
        grabbedByTitan = hand;

        // Disable movement while grabbed
        if (movement != null) movement.enabledMovement = false;
        if (odmMovement != null) odmMovement.enabled = false;

        // Parent player to titan hand
        transform.SetParent(hand);
    }

    public void OnReleased()
    {
        isGrabbed = false;
        grabbedByTitan = null;

        // Unparent from titan
        transform.SetParent(null);

        // Re-enable movement
        if (movement != null) movement.enabledMovement = true;
        if (odmMovement != null) odmMovement.enabled = true;
    }

    private void ReloadScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }
}
