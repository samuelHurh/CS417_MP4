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
        int numTicks = (int)(Duration / healingTickDelay);
        int currTicks = 0;
        while (currTicks < numTicks)
        {
            if (IsEnemyTouchingHealingCapsule())
            {
                Debug.Log("Player touching damage AOE");
            }

            yield return new WaitForSeconds(healingTickDelay);
            currTicks++;
        }
    }

    private bool IsEnemyTouchingHealingCapsule()
    {
        if (myVisual == null)
        {
            return false;
        }

        CapsuleCollider capsuleCollider = myVisual.GetComponentInChildren<CapsuleCollider>();

        if (capsuleCollider == null)
        {
            return false;
        }

        Transform capsuleTransform = capsuleCollider.transform;
        Vector3 center = capsuleTransform.TransformPoint(capsuleCollider.center);
        Vector3 axis = GetCapsuleWorldAxis(capsuleCollider);
        float height = GetCapsuleWorldHeight(capsuleCollider);
        float radius = GetCapsuleWorldRadius(capsuleCollider);
        float cylinderHalfHeight = Mathf.Max(0f, (height * 0.5f) - radius);

        Vector3 pointA = center + axis * cylinderHalfHeight;
        Vector3 pointB = center - axis * cylinderHalfHeight;
        Collider[] hits = Physics.OverlapCapsule(pointA, pointB, radius, enemyLayerMask);

        return hits.Length > 0;
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
