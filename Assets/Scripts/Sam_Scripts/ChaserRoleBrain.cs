using UnityEngine;
using TMPro;

public class ChaserRoleBrain : EnemyRoleBrain
{

    
    // This function applies close-range pursuit logic for the chaser role each frame.
    public override void Tick()
    {
        if (controller == null)
        {
            return;
        }

        if (controller.HasLineOfSight && controller.PlayerTarget != null)
        {
            if (controller.IsWithinAttackRange())
            {
                controller.ChangeActionState(EnemyActionState.Attack);
                controller.StopMoving();
            }
            else
            {
                controller.ChangeActionState(EnemyActionState.MoveToPlayer);
                controller.MoveTo(controller.PlayerTarget.position);
            }

            return;
        }

        if (controller.HasLastKnownPlayerPosition)
        {
            if (controller.HasReachedPosition(controller.LastKnownPlayerPosition))
            {
                controller.ChangeActionState(EnemyActionState.Search);
                controller.StopMoving();
            }
            else
            {
                controller.ChangeActionState(EnemyActionState.MoveToSearchArea);
                controller.MoveTo(controller.LastKnownPlayerPosition);
            }

            return;
        }

        controller.ChangeActionState(EnemyActionState.ReturnToAnchor);
        controller.MoveTo(controller.AnchorPosition);
    }
}
