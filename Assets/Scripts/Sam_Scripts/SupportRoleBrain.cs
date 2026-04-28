using System.Collections;
using UnityEngine;

public class SupportRoleBrain : EnemyRoleBrain
{
    [Header("Targeting")]
    [SerializeField] private Transform eyePoint;
    [SerializeField] private LayerMask lineOfSightMask = Physics.DefaultRaycastLayers;
    [SerializeField] private float maxThrowRange = 10f;
    [SerializeField] private GameObject projectileLobTargetPrefab;

    [Header("Timing")]
    [SerializeField] private float throwWindup = 0.5f;
    [SerializeField] private float minTimeBetweenThrows = 4f;
    [SerializeField] private float repollDelay = 0.1f;

    private Coroutine supportActionRoutine;

    public override void Initialize(EnemyAIController owningController)
    {
        base.Initialize(owningController);

        if (supportActionRoutine == null)
        {
            supportActionRoutine = StartCoroutine(SupportActionLoop());
        }
    }

    public override void Tick()
    {
        if (controller == null)
        {
            return;
        }

        if (controller.PlayerTarget == null)
        {
            controller.ChangeActionState(EnemyActionState.Idle);
            controller.StopMoving();
        }
    }

    private IEnumerator SupportActionLoop()
    {
        while (true)
        {
            if (controller == null || controller.SquadBlackboard == null || controller.PlayerTarget == null)
            {
                yield return null;
                continue;
            }

            SupportDecision decision = controller.SquadBlackboard.PollTable();
            Transform target = ResolveTarget(decision);

            if (target == null)
            {
                yield return new WaitForSeconds(repollDelay);
                continue;
            }

            yield return ExecuteDecision(decision, target);
            yield return new WaitForSeconds(minTimeBetweenThrows);
        }
    }

    private Transform ResolveTarget(SupportDecision decision)
    {
        switch (decision)
        {
            case SupportDecision.Heal:
                return ResolveEnemyTarget(controller.SquadBlackboard.GetLowestHealthLivingEnemy(controller));
            case SupportDecision.DD:
                return ResolveEnemyTarget(controller.SquadBlackboard.GetRandomLivingEnemy(controller));
            default:
                return controller.PlayerTarget;
        }
    }

    private static Transform ResolveEnemyTarget(EnemyAIController enemy)
    {
        if (enemy == null || enemy.IsDead)
        {
            return null;
        }

        return enemy.transform;
    }

    private IEnumerator ExecuteDecision(SupportDecision decision, Transform target)
    {
        while (target != null && IsTargetStillValid(decision, target))
        {
            if (IsReadyToThrowAt(decision, target))
            {
                controller.StopMoving();
                FaceTarget(target);
                controller.ChangeActionState(decision == SupportDecision.Attack ? EnemyActionState.Attack : EnemyActionState.Support);

                yield return new WaitForSeconds(throwWindup);

                if (target != null && IsTargetStillValid(decision, target))
                {
                    ThrowDecisionProjectile(decision, target.position);
                }

                yield break;
            }

            MoveTowardTarget(decision, target);
            yield return null;
        }
    }

    private bool IsTargetStillValid(SupportDecision decision, Transform target)
    {
        if (decision == SupportDecision.Attack)
        {
            return target == controller.PlayerTarget;
        }

        EnemyAIController enemy = target.GetComponent<EnemyAIController>();
        return enemy != null && !enemy.IsDead;
    }

    private bool IsReadyToThrowAt(SupportDecision decision, Transform target)
    {
        if (Vector3.Distance(transform.position, target.position) > maxThrowRange)
        {
            return false;
        }

        return decision != SupportDecision.Attack || HasLineOfSightTo(target);
    }

    private void MoveTowardTarget(SupportDecision decision, Transform target)
    {
        controller.ChangeActionState(decision == SupportDecision.Attack ? EnemyActionState.MoveToPlayer : EnemyActionState.Support);
        controller.MoveTo(target.position);
    }

    private bool HasLineOfSightTo(Transform target)
    {
        Vector3 origin = eyePoint != null ? eyePoint.position : transform.position + Vector3.up;
        Vector3 toTarget = target.position - origin;

        if (!Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, toTarget.magnitude, lineOfSightMask))
        {
            return true;
        }

        return hit.transform == target || hit.transform.IsChildOf(target);
    }

    private void FaceTarget(Transform target)
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(toTarget);
    }

    private void ThrowDecisionProjectile(SupportDecision decision, Vector3 targetPosition)
    {
        if ((decision == SupportDecision.Attack || decision == SupportDecision.DD) && projectileLobTargetPrefab != null)
        {
            Instantiate(projectileLobTargetPrefab, targetPosition, Quaternion.identity);
        }

        Debug.Log($"Support throws {decision} projectile at {targetPosition}");
    }
}
