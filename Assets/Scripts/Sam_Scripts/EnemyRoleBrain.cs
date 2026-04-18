using UnityEngine;

public abstract class EnemyRoleBrain : MonoBehaviour
{
    // This reference stores the owning controller so the role brain can issue movement and state decisions.
    protected EnemyAIController controller;

    // This function injects the owning controller when the role brain is attached or reassigned.
    public virtual void Initialize(EnemyAIController owningController)
    {
        controller = owningController;
    }

    // This function advances the role-specific decision logic for a single frame.
    public abstract void Tick();
}
