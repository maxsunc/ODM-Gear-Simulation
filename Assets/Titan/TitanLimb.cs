using UnityEngine;

public enum LimbType
{
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg,
    Eyes
}

public class TitanLimb : MonoBehaviour
{
    [Header("Limb Settings")]
    public LimbType limbType;
    public float maxHP = 50f;
    public float currentHP;

    [Header("References")]
    public MeshRenderer limbMeshRenderer;

    [HideInInspector] public bool isDestroyed = false;

    private TitanController titanController;

    void Start()
    {
        currentHP = maxHP;
        titanController = GetComponentInParent<TitanController>();
    }

    public void TakeDamage(float damage)
    {
        if (isDestroyed) return;

        currentHP -= damage;

        if (currentHP <= 0f)
        {
            DestroyLimb();
        }
    }

    void DestroyLimb()
    {
        isDestroyed = true;

        // Disable visuals
        if (limbMeshRenderer != null)
        {
            limbMeshRenderer.enabled = false;
        }

        // Disable this trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        // Notify the titan controller
        if (titanController != null)
        {
            titanController.OnLimbDestroyed(limbType);
        }
    }
}
