using UnityEngine;
using System.Collections;

public class HealAOEZone : AOEZoneBase
{
    [Header("Healing")]
    [SerializeField] private float healPerTick = 10f;
    [SerializeField] private float healTickInterval = 1f;
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private float healingTickDelay;

    protected override void OnAOEActivated()
    {
        Debug.Log("Heal AOE activated");
        myVisual.SetActive(true);
        StartCoroutine(Tick());
        
    }

    protected override void OnAOEExpired()
    {
        Debug.Log("Heal AOE expired");
    }

    public IEnumerator Tick()
    {
        // Defensive clamp: healingTickDelay = 0 (Inspector default) would cause
        // Duration / 0 = Infinity → numTicks runaway → tick every frame indefinitely.
        float effectiveDelay = Mathf.Max(0.5f, healingTickDelay);

        int numTicks = (int)(Duration / effectiveDelay);
        int currTicks = 0;
        while (currTicks < numTicks)
        {
            HealEnemiesTouchingCapsule();

            yield return new WaitForSeconds(effectiveDelay);
            currTicks++;
        }
    }

    private void HealEnemiesTouchingCapsule()
    {
        if (myVisual == null) return;

        CapsuleCollider capsuleCollider = myVisual.GetComponentInChildren<CapsuleCollider>();
        if (capsuleCollider == null) return;

        Transform capsuleTransform = capsuleCollider.transform;
        Vector3 center = capsuleTransform.TransformPoint(capsuleCollider.center);
        Vector3 axis = GetCapsuleWorldAxis(capsuleCollider);
        float height = GetCapsuleWorldHeight(capsuleCollider);
        float radius = GetCapsuleWorldRadius(capsuleCollider);
        float cylinderHalfHeight = Mathf.Max(0f, (height * 0.5f) - radius);

        Vector3 pointA = center + axis * cylinderHalfHeight;
        Vector3 pointB = center - axis * cylinderHalfHeight;

        // Layer-mask fallback: if Inspector left enemyLayerMask = 0 (default for unset
        // LayerMask), no enemies would ever match. Fall back to the EnemyHitbox layer
        // if defined; otherwise broaden to all layers so at least we don't silently
        // drop every heal.
        int effectiveMask = enemyLayerMask.value;
        if (effectiveMask == 0)
        {
            int enemyHitboxLayer = LayerMask.NameToLayer("EnemyHitbox");
            effectiveMask = enemyHitboxLayer >= 0
                ? (1 << enemyHitboxLayer)
                : Physics.AllLayers;
        }

        Collider[] hits = Physics.OverlapCapsule(pointA, pointB, radius, effectiveMask);

        // Track healed enemies so a multi-collider enemy is only healed once per tick.
        var seen = new System.Collections.Generic.HashSet<EnemyHealthBar>();
        foreach (Collider hit in hits)
        {
            // Three-step walk: same GO, ancestors, descendants. Robust against any
            // reasonable enemy hierarchy (EnemyHealthBar on root, on hitbox child, etc.)
            EnemyHealthBar bar = hit.GetComponent<EnemyHealthBar>()
                              ?? hit.GetComponentInParent<EnemyHealthBar>()
                              ?? hit.GetComponentInChildren<EnemyHealthBar>();
            if (bar == null) continue;
            if (!seen.Add(bar)) continue;

            bar.ApplyHeal(healPerTick);
        }

        // Diagnostic — surface silent failures. Logs only when there are hits but no
        // EnemyHealthBars were resolved (the most-likely silent-fail case).
        if (hits.Length > 0 && seen.Count == 0)
        {
            Debug.LogWarning(
                $"[HealAOEZone] OverlapCapsule found {hits.Length} colliders but no " +
                "EnemyHealthBar component was resolvable from any of them. Check that each " +
                "enemy prefab has an EnemyHealthBar component on the same GameObject as the " +
                "collider, or on an ancestor / descendant.",
                this);
        }
    }

    private static Vector3 GetCapsuleWorldAxis(CapsuleCollider capsuleCollider)
    {
        switch (capsuleCollider.direction)
        {
            case 0:
                return capsuleCollider.transform.right;
            case 2:
                return capsuleCollider.transform.forward;
            default:
                return capsuleCollider.transform.up;
        }
    }

    private static float GetCapsuleWorldHeight(CapsuleCollider capsuleCollider)
    {
        Vector3 lossyScale = capsuleCollider.transform.lossyScale;

        switch (capsuleCollider.direction)
        {
            case 0:
                return capsuleCollider.height * Mathf.Abs(lossyScale.x);
            case 2:
                return capsuleCollider.height * Mathf.Abs(lossyScale.z);
            default:
                return capsuleCollider.height * Mathf.Abs(lossyScale.y);
        }
    }

    private static float GetCapsuleWorldRadius(CapsuleCollider capsuleCollider)
    {
        Vector3 lossyScale = capsuleCollider.transform.lossyScale;

        switch (capsuleCollider.direction)
        {
            case 0:
                return capsuleCollider.radius * Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
            case 2:
                return capsuleCollider.radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
            default:
                return capsuleCollider.radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
        }
    }
}
