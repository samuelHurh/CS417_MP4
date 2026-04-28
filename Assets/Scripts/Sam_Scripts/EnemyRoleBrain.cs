using UnityEngine;

public abstract class EnemyRoleBrain : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    // This reference stores the owning controller so the role brain can issue movement and state decisions.
    protected EnemyAIController controller;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float HealthPercent => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public bool IsAlive => currentHealth > 0f;

    protected virtual void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    // This function injects the owning controller when the role brain is attached or reassigned.
    public virtual void Initialize(EnemyAIController owningController)
    {
        controller = owningController;
    }

    public virtual void TakeDamage(float damageAmount)
    {
        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);

        if (currentHealth <= 0f)
        {
            controller?.MarkDead();
        }
    }

    public virtual void Heal(float healAmount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
    }

    // This function advances the role-specific decision logic for a single frame.
    public abstract void Tick();
}
