using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class SupportRoleBrain : EnemyRoleBrain
{
    [Header("Targeting")]
    [SerializeField] private Transform eyePoint;
    [SerializeField] private LayerMask lineOfSightMask = Physics.DefaultRaycastLayers;
    [SerializeField] private float maxThrowRange = 10f;
    [SerializeField] private float targetGroundingSampleRadius = 3f;
    [SerializeField] private GameObject projectileLobTargetPrefab;
    [SerializeField] private GameObject damageAOEPrefab;
    [SerializeField] private GameObject healAOEPrefab;

    [Header("Timing")]
    [SerializeField] private float throwWindup = 0.5f;
    [SerializeField] private float minTimeBetweenThrows = 4f;
    [SerializeField] private float repollDelay = 0.1f;

    [Header("Projectile Visual")]
    [SerializeField] private float lobTravelTime = 0.8f;
    [SerializeField] private float arcHeightPerMeter = 0.25f;
    [SerializeField] private float minArcHeight = 1f;
    [SerializeField] private float maxArcHeight = 5f;

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

        RaycastHit[] hits = Physics.RaycastAll(origin, toTarget.normalized, toTarget.magnitude, lineOfSightMask);
        System.Array.Sort(hits, (leftHit, rightHit) => leftHit.distance.CompareTo(rightHit.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return IsHitOnTarget(hit.transform, target);
        }

        return true;
    }

    private static bool IsHitOnTarget(Transform hitTransform, Transform target)
    {
        return hitTransform == target ||
               hitTransform.IsChildOf(target) ||
               target.IsChildOf(hitTransform) ||
               hitTransform.root == target.root;
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
        Vector3 groundedTargetPosition = GetGroundedTargetPosition(targetPosition);

        GameObject aoePrefab = GetAOEPrefab(decision);

        if (aoePrefab != null)
        {
            Instantiate(aoePrefab, groundedTargetPosition, Quaternion.identity);
        }

        if (projectileLobTargetPrefab != null)
        {
            SpawnLobProjectileVisual(groundedTargetPosition);
        }

        Debug.Log($"Support throws {decision} projectile at {groundedTargetPosition}");
    }

    private GameObject GetAOEPrefab(SupportDecision decision)
    {
        switch (decision)
        {
            case SupportDecision.Heal:
                return healAOEPrefab;
            default:
                return damageAOEPrefab;
        }
    }

    private void SpawnLobProjectileVisual(Vector3 targetPosition)
    {
        Vector3 startPosition = eyePoint != null ? eyePoint.position : transform.position + Vector3.up;
        GameObject projectileVisual = Instantiate(projectileLobTargetPrefab, startPosition, Quaternion.identity);
        StartCoroutine(LobProjectileVisual(projectileVisual.transform, startPosition, targetPosition));
    }

    private IEnumerator LobProjectileVisual(Transform projectileTransform, Vector3 startPosition, Vector3 targetPosition)
    {
        float elapsedTime = 0f;
        float distance = Vector3.Distance(startPosition, targetPosition);
        float arcHeight = Mathf.Clamp(distance * arcHeightPerMeter, minArcHeight, maxArcHeight);
        float travelTime = Mathf.Max(0.01f, lobTravelTime);

        while (elapsedTime < travelTime && projectileTransform != null)
        {
            float t = elapsedTime / travelTime;
            Vector3 nextPosition = Vector3.Lerp(startPosition, targetPosition, t);
            nextPosition.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            projectileTransform.position = nextPosition;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (projectileTransform != null)
        {
            projectileTransform.position = targetPosition;
            Destroy(projectileTransform.gameObject);
        }
    }

    private Vector3 GetGroundedTargetPosition(Vector3 targetPosition)
    {
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, targetGroundingSampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return targetPosition;
    }
}
