using System.Collections;
using UnityEngine;

public class DamageAOEZone : AOEZoneBase
{
    [Header("Damage")]
    [SerializeField] private float damagePerTick = 10f;
    [SerializeField] private float damageTickInterval = 1f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] float damageTickDelay;
    [SerializeField] private string damageSourceId = "damage_aoe";

    protected override void OnAOEActivated()
    {
        Debug.Log("Damage AOE activated");
        StartCoroutine(DelayCoroutine());
    }

    protected override void OnAOEExpired()
    {
        Debug.Log("Damage AOE expired");
        myVisual.SetActive(false);
    }

    public IEnumerator DelayCoroutine()
    {
        yield return new WaitForSeconds(ActivationDelay);
        myVisual.SetActive(true);
        StartCoroutine(Tick());
    }

    public IEnumerator Tick()
    {
        // Defensive clamp: damageTickDelay = 0 (Inspector default) would cause
        // Duration / 0 = Infinity → numTicks runaway → tick every frame indefinitely.
        float effectiveDelay = Mathf.Max(0.5f, damageTickDelay);

        int numTicks = (int)(Duration / effectiveDelay);
        int currTicks = 0;
        while (currTicks < numTicks)
        {
            DamagePlayersTouchingCapsule();

            yield return new WaitForSeconds(effectiveDelay);
            currTicks++;
        }
    }

    private void DamagePlayersTouchingCapsule()
    {
        if (myVisual == null)
        {
            return;
        }

        CapsuleCollider capsuleCollider = myVisual.GetComponentInChildren<CapsuleCollider>();

        if (capsuleCollider == null)
        {
            return;
        }

        Transform capsuleTransform = capsuleCollider.transform;
        Vector3 center = capsuleTransform.TransformPoint(capsuleCollider.center);
        Vector3 axis = GetCapsuleWorldAxis(capsuleCollider);
        float height = GetCapsuleWorldHeight(capsuleCollider);
        float radius = GetCapsuleWorldRadius(capsuleCollider);
        float cylinderHalfHeight = Mathf.Max(0f, (height * 0.5f) - radius);

        Vector3 pointA = center + axis * cylinderHalfHeight;
        Vector3 pointB = center - axis * cylinderHalfHeight;
        Collider[] hits = Physics.OverlapCapsule(
            pointA,
            pointB,
            radius,
            PlayerDamageHelpers.PlayerHitboxInclusiveMask(playerLayerMask));

        foreach (Collider hit in hits)
        {
            if (PlayerDamageHelpers.TryDamagePlayer(hit, damagePerTick, damageSourceId, hit.ClosestPoint(center), this))
            {
                Debug.Log("Player touching damage AOE");
                return;
            }
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
