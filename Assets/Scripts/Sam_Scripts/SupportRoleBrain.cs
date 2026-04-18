using UnityEngine;

public class SupportRoleBrain : EnemyRoleBrain
{
    // This distance defines how much spacing the support unit wants to keep from the player.
    [SerializeField] private float supportDistance = 10f;

    // This function applies spacing and utility-oriented logic for the support role each frame.
    public override void Tick()
    {
        if (controller == null)
        {
            return;
        }

        if (controller.HasLineOfSight && controller.PlayerTarget != null)
        {
            float playerDistance = Vector3.Distance(controller.transform.position, controller.PlayerTarget.position);

            if (playerDistance < supportDistance)
            {
                controller.ChangeActionState(EnemyActionState.Reposition);
                controller.StopMoving();
            }
            else
            {
                controller.ChangeActionState(EnemyActionState.Support);
                controller.StopMoving();
            }

            return;
        }

        if (controller.HasLastKnownPlayerPosition)
        {
            controller.ChangeActionState(EnemyActionState.Search);
            controller.MoveTo(controller.LastKnownPlayerPosition);
            return;
        }

        controller.ChangeActionState(EnemyActionState.ReturnToAnchor);
        controller.MoveTo(controller.AnchorPosition);
    }
}
