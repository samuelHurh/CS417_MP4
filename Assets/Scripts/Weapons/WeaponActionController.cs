using UnityEngine;

public class WeaponActionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponCore weaponCore;
    [SerializeField] private Transform actionTransform;
    [SerializeField] private Transform forwardLimit;
    [SerializeField] private Transform rearLimit;

    [Header("Behavior")]
    [SerializeField, Range(0f, 1f)] private float rackThreshold = 0.8f;
    [SerializeField, Range(0f, 0.25f)] private float feedClosedThreshold = 0.05f;
    [SerializeField] private float settleEpsilon = 0.001f;

    private float currentTravel;
    private bool thresholdReachedThisCycle;
    private bool wasClosedLastFrame;

    private void Awake()
    {
        if (actionTransform == null)
        {
            actionTransform = transform;
        }
        currentTravel = GetProjectedTravel();
        wasClosedLastFrame = currentTravel <= settleEpsilon;
    }

    private void LateUpdate()
    {
        float maxTravel = GetMaxTravel();
        if (maxTravel <= Mathf.Epsilon || actionTransform == null || forwardLimit == null || rearLimit == null)
        {
            return;
        }

        currentTravel = GetProjectedTravel();
        bool isClosed = currentTravel <= GetClosedTravelThreshold(maxTravel);
        bool crossedRackThreshold = currentTravel + settleEpsilon >= maxTravel * Mathf.Clamp01(rackThreshold);

        if (crossedRackThreshold && !thresholdReachedThisCycle)
        {
            thresholdReachedThisCycle = true;
            weaponCore?.SetActionLockedOpen(false);
            weaponCore?.EjectChamberedRound();
        }

        if (thresholdReachedThisCycle && isClosed && !wasClosedLastFrame)
        {
            if (weaponCore != null && weaponCore.ShouldLockOpenOnRelease())
            {
                weaponCore.SetActionLockedOpen(true);
            }
            else
            {
                weaponCore?.SetActionLockedOpen(false);
                weaponCore?.TryFeedRoundToChamber();
            }

            thresholdReachedThisCycle = false;
        }

        if (!thresholdReachedThisCycle && isClosed && weaponCore != null && !weaponCore.ShouldLockOpenOnRelease())
        {
            weaponCore.SetActionLockedOpen(false);
        }

        wasClosedLastFrame = isClosed;
    }

    private float GetProjectedTravel()
    {
        Vector3 forwardLocal = GetForwardLocalPoint();
        Vector3 rearLocal = GetRearLocalPoint();
        Vector3 axisLocal = rearLocal - forwardLocal;
        float maxTravel = axisLocal.magnitude;

        if (maxTravel <= Mathf.Epsilon)
        {
            return 0f;
        }

        Vector3 currentLocal = GetParentTransform().InverseTransformPoint(actionTransform.position);
        float projectedTravel = Vector3.Dot(currentLocal - forwardLocal, axisLocal.normalized);
        return Mathf.Clamp(projectedTravel, 0f, maxTravel);
    }

    private float GetMaxTravel()
    {
        return Vector3.Distance(GetForwardLocalPoint(), GetRearLocalPoint());
    }

    private float GetClosedTravelThreshold(float maxTravel)
    {
        return Mathf.Max(settleEpsilon, maxTravel * feedClosedThreshold);
    }

    private Vector3 GetForwardLocalPoint()
    {
        return GetParentTransform().InverseTransformPoint(forwardLimit.position);
    }

    private Vector3 GetRearLocalPoint()
    {
        return GetParentTransform().InverseTransformPoint(rearLimit.position);
    }

    private Transform GetParentTransform()
    {
        return actionTransform != null && actionTransform.parent != null ? actionTransform.parent : transform;
    }
}
