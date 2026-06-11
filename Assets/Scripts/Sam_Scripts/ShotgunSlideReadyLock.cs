using BNG;
using UnityEngine;

public sealed class ShotgunSlideReadyLock : WeaponSlide
{
    [SerializeField] private RaycastWeapon weapon;
    [SerializeField] private Grabbable slideGrabbable;

    private Vector3 readyLocalPosition;

    private void Awake()
    {
        ResolveReferences();
        readyLocalPosition = transform.localPosition;
    }

    private void LateUpdate()
    {
        if (!ShouldLockReadySlide())
        {
            return;
        }

        transform.localPosition = readyLocalPosition;
    }

    public override void UpdateHeldSlide()
    {
        if (ShouldLockReadySlide())
        {
            transform.localPosition = readyLocalPosition;
            return;
        }

        base.UpdateHeldSlide();
    }

    protected override bool CanTriggerSlideBackCharge()
    {
        ResolveReferences();
        return slideGrabbable != null && slideGrabbable.BeingHeld;
    }

    private bool ShouldLockReadySlide()
    {
        ResolveReferences();
        return weapon != null
            && weapon.BulletInChamber
            && slideGrabbable != null
            && !slideGrabbable.BeingHeld;
    }

    private void ResolveReferences()
    {
        if (weapon == null)
        {
            weapon = GetComponentInParent<RaycastWeapon>();
        }

        if (slideGrabbable == null)
        {
            slideGrabbable = GetComponent<Grabbable>();
        }
    }
}
