using UnityEngine;

public class ShooterRoleBrain : EnemyRoleBrain
{
    // This distance defines the outer edge of the shooter's preferred firing band.
    [SerializeField] private float preferredRange = 8f;
    // This distance defines when the shooter feels too close to the player and should reposition.
    [SerializeField] private float minimumRange = 4f;

    // This function applies ranged positioning and firing logic for the shooter role each frame.
    public override void Tick()
    {
        if (controller == null)
        {
            return;
        }

        if (controller.HasLineOfSight && controller.PlayerTarget != null)
        {
            float playerDistance = Vector3.Distance(controller.transform.position, controller.PlayerTarget.position);

            if (playerDistance < minimumRange)
            {
                controller.ChangeActionState(EnemyActionState.Reposition);
                controller.StopMoving();
            }
            else if (playerDistance > preferredRange)
            {
                controller.ChangeActionState(EnemyActionState.MoveToPlayer);
                controller.MoveTo(controller.PlayerTarget.position);
            }
            else
            {
                controller.ChangeActionState(EnemyActionState.Attack);
                controller.StopMoving();
            }

            return;
        }

        if (controller.HasLastKnownPlayerPosition)
        {
            controller.ChangeActionState(EnemyActionState.MoveToSearchArea);
            controller.MoveTo(controller.LastKnownPlayerPosition);
            return;
        }

        controller.ChangeActionState(EnemyActionState.ReturnToAnchor);
        controller.MoveTo(controller.AnchorPosition);
    }
}
