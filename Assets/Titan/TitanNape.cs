using UnityEngine;

public class TitanNape : MonoBehaviour
{
    [Header("Nape Settings")]
    public float maxHP = 30f;
    public float currentHP;

    private TitanController titanController;

    void Start()
    {
        currentHP = maxHP;
        titanController = GetComponentInParent<TitanController>();
    }

    public void TakeDamage(float damage)
    {
        if (titanController != null && titanController.currentState == TitanState.Dead)
            return;

        currentHP -= damage;

        if (currentHP <= 0f)
        {
            if (titanController != null)
            {
                titanController.Die();
            }
        }
    }
}
