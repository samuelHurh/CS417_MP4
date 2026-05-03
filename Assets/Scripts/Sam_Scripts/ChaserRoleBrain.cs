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
        Collider[] hits = Physics.OverlapSphere(
            center,
            attackRadius,
            PlayerDamageHelpers.PlayerHitboxInclusiveMask(playerLayerMask));

        foreach (Collider hit in hits)
        {
            if (PlayerDamageHelpers.TryDamagePlayer(hit, attackDamage, damageSourceId, hit.ClosestPoint(center), this))
            {
                return;
            }
        }
    }
}

public static class PlayerDamageHelpers
{
    public static int PlayerHitboxInclusiveMask(LayerMask configuredMask)
    {
        int playerHitboxLayer = LayerMask.NameToLayer("PlayerHitbox");
        int playerHitboxMask = playerHitboxLayer == -1 ? 0 : 1 << playerHitboxLayer;

        if (configuredMask.value == 0)
        {
            return playerHitboxMask != 0 ? playerHitboxMask : Physics.DefaultRaycastLayers;
        }

        return configuredMask.value | playerHitboxMask;
    }

    public static bool TryDamagePlayer(Collider hitCollider, float damage, string sourceId, Vector3 hitPosition, Object logContext)
    {
        if (hitCollider == null)
        {
            return false;
        }

        if (TryGetHittable(hitCollider, out IHittable hittable))
        {
            DamageEvent damageEvent = new DamageEvent(damage, sourceId, false, hitPosition);
            hittable.TakeDamage(in damageEvent);
            return true;
        }

        if (!IsPlayerHitboxCollider(hitCollider))
        {
            return false;
        }

        Debug.Log($"Player took {damage} damage", logContext);
        return true;
    }

    public static bool IsPlayerHitboxCollider(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        int playerHitboxLayer = LayerMask.NameToLayer("PlayerHitbox");
        return (playerHitboxLayer != -1 && hitCollider.gameObject.layer == playerHitboxLayer) ||
               hitCollider.name == "PlayerCollider";
    }

    private static bool TryGetHittable(Collider hitCollider, out IHittable hittable)
    {
        if (hitCollider.TryGetComponent(out hittable))
        {
            return true;
        }

        hittable = hitCollider.GetComponentInParent<IHittable>();
        return hittable != null;
    }
}
