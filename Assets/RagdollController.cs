using UnityEngine;

public class RagdollController : MonoBehaviour
{
    public Animator animator;
    public Rigidbody rootRigidbody; // the one that controls movement when not ragdolling
    public Collider rootCollider;

    private Rigidbody[] limbRigidbodies;
    private Collider[] limbColliders;

    void Awake()
    {
        // Get all rigidbodies & colliders in children, excluding root
        limbRigidbodies = GetComponentsInChildren<Rigidbody>();
        limbColliders = GetComponentsInChildren<Collider>();

        // Filter out the root rigidbody and collider
        limbRigidbodies = System.Array.FindAll(limbRigidbodies, rb => rb != rootRigidbody);
        limbColliders = System.Array.FindAll(limbColliders, col => col != rootCollider);

        // Start in animated state
        SetRagdollState(false);
    }

    public void SetRagdollState(bool isRagdoll)
    {
        animator.enabled = !isRagdoll;
        rootRigidbody.isKinematic = isRagdoll;
        rootCollider.enabled = !isRagdoll;

        foreach (var rb in limbRigidbodies)
            rb.isKinematic = !isRagdoll;

        // foreach (var col in limbColliders)
        //     col.enabled = isRagdoll;
    }

    public void EnableRagdoll() => SetRagdollState(true);
    public void DisableRagdoll() => SetRagdollState(false);
}
