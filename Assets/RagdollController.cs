using UnityEngine;

public class RagdollController : MonoBehaviour
{
    public Animator animator;
    public Rigidbody rootRigidbody; // the one that controls movement when not ragdolling
    // public Collider rootCollider; // optional, if you want the root to have a collider when not ragdolling
    public bool startInRagdoll = false; // if true, starts in ragdoll state
    private Rigidbody[] limbRigidbodies;
    private Collider[] limbColliders;
    private CharacterJoint[] joints;

    void Awake()
    {
        // Get all rigidbodies & colliders in children, excluding root
        limbRigidbodies = GetComponentsInChildren<Rigidbody>();
        limbColliders = GetComponentsInChildren<Collider>();
        joints = GetComponentsInChildren<CharacterJoint>();


        // Filter out the root rigidbody and collider
        limbRigidbodies = System.Array.FindAll(limbRigidbodies, rb => rb != rootRigidbody);
        limbColliders = System.Array.FindAll(limbColliders, col => col != null);

        // Start in animated state
        SetRagdollState(startInRagdoll);
    }

    public void SetRagdollState(bool isRagdoll)
    {
        animator.enabled = !isRagdoll;
        rootRigidbody.isKinematic = isRagdoll;
        // rootCollider.enabled = !isRagdoll; 

        foreach (var joint in joints)
            joint.enableCollision = isRagdoll;

        // foreach (var col in limbColliders)
        //     col.enabled = isRagdoll;
        // keep all colliders enabled for hit detection, but only enable physics on limbs when ragdolling

        foreach (var rb in limbRigidbodies)
        {
            rb.isKinematic = !isRagdoll;
            rb.detectCollisions = isRagdoll;
            rb.useGravity = isRagdoll;
        }
    }

    public void EnableRagdoll() => SetRagdollState(true);
    public void DisableRagdoll() => SetRagdollState(false);
}
