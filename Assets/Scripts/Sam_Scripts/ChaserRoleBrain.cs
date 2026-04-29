using System.Collections;
using JerryScripts.Foundation.Damage;
using UnityEngine;

public class ChaserRoleBrain : EnemyRoleBrain
{
    [SerializeField] private float resumeChaseBuffer = 0.5f;
    [SerializeField] private Transform attackCenter;
    [SerializeField] private float attackRadius;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackWindup = 0.5f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private string damageSourceId = "chaser";

    private bool isAttacking;
    private Coroutine attackRoutine;

    public override void Tick()
    {
        if (controller == null)
        {
            return;
        }

        if (controller.PlayerTarget == null)
        {
            ExitAttackState();
            controller.ChangeActionState(EnemyActionState.Idle);
            controller.StopMoving();
            return;
        }

        float distanceToPlayer = Vector3.Distance(controller.transform.position, controller.PlayerTarget.position);

        if (isAttacking)
        {
            if (distanceToPlayer <= controller.AttackRange + resumeChaseBuffer)
            {
                controller.ChangeActionState(EnemyActionState.Attack);
                controller.StopMoving();
                return;
            }

            isAttacking = false;
        }

        if (distanceToPlayer <= controller.AttackRange)
        {
            EnterAttackState();
            controller.ChangeActionState(EnemyActionState.Attack);
            controller.StopMoving();
            return;
        }

        ExitAttackState();
        controller.ChangeActionState(EnemyActionState.MoveToPlayer);
        controller.MoveTo(controller.PlayerTarget.position);
    }

    private void EnterAttackState()
    {
        if (isAttacking)
        {
            return;
        }

        isAttacking = true;
        attackRoutine = StartCoroutine(AttackCoroutine());
    }

    private void ExitAttackState()
    {
        isAttacking = false;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }

        attackRoutine = null;
    }

    public IEnumerator AttackCoroutine()
    {
        yield return new WaitForSeconds(attackWindup);

        while (isAttacking)
        {
            TryDamagePlayerInAttackRange();
            yield return new WaitForSeconds(attackCooldown);
        }

        attackRoutine = null;
    }

    private void TryDamagePlayerInAttackRange()
    {
        Vector3 center = attackCenter != null ? attackCenter.position : transform.position;
        Collider[] hits = Physics.OverlapSphere(center, attackRadius, playerLayerMask);

        foreach (Collider hit in hits)
        {
            if (TryGetHittable(hit, out IHittable hittable))
            {
                DamageEvent damageEvent = new DamageEvent(attackDamage, damageSourceId, false, hit.ClosestPoint(center));
                hittable.TakeDamage(in damageEvent);
                return;
            }
        }
    }

    private static bool TryGetHittable(Collider hit, out IHittable hittable)
    {
        if (hit.TryGetComponent(out hittable))
        {
            return true;
        }

        hittable = hit.GetComponentInParent<IHittable>();
        return hittable != null;
    }
}
