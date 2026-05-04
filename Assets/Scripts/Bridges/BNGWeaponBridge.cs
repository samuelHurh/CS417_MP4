using BNG;
using JerryScripts.Foundation.Player;
using UnityEngine;

/// <summary>
/// Singleton bridge between BNG/Sam's weapon side and Jerry's HUD.
/// Lives on the <c>_Systems</c> GameObject (one per scene). Each frame:
/// finds whichever <c>RaycastWeapon</c> in the scene is currently held
/// (sibling <c>Grabbable.BeingHeld</c> is true), reads its ammo + max +
/// <c>GeneratedWeaponManager</c> stats, and publishes to <see cref="HUDWeaponBus"/>.
///
/// <para>When the held weapon changes (player drops one, picks up another), the
/// snapshot is republished for the new weapon — fixes the bug where switching
/// pistols left the HUD showing the previous weapon's stats and ammo count.</para>
///
/// <para><b>Wiring</b>: attach exactly ONCE on the scene's <c>_Systems</c> GameObject
/// (NOT on individual pistol prefabs). Each pistol just needs the standard BNG
/// components + Sam's <c>GeneratedWeaponManager</c> — no bridge component required.</para>
///
/// <para><b>Asmdef boundary note</b>: lives in default Assembly-CSharp because it
/// references BNG types and Sam's GeneratedWeaponManager.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class BNGWeaponBridge : MonoBehaviour
{
    private RaycastWeapon _currentWeapon;
    private GeneratedWeaponManager _currentManager;

    private int _lastPublishedAmmo = -1;
    private int _lastPublishedMag = -1;
    private bool _lastPublishedHeld;
    private bool _hasInitialPublish;

    private void Update()
    {
        // Find currently-held weapon (any RaycastWeapon whose Grabbable is BeingHeld).
        RaycastWeapon nowHeld = FindHeldWeapon();
        bool currentlyHeld = nowHeld != null;

        // Detect weapon switch — clear or set _currentManager and publish fresh stats.
        if (nowHeld != _currentWeapon)
        {
            _currentWeapon = nowHeld;
            _currentManager = nowHeld != null
                ? nowHeld.GetComponent<GeneratedWeaponManager>()
                : null;
            PublishStatsForCurrent();
        }

        // Ammo publish (current + max).
        int ammoNow = currentlyHeld ? _currentWeapon.GetBulletCount() : 0;
        int magNow  = currentlyHeld ? _currentWeapon.GetMaxBulletCount() : 0;

        if (!_hasInitialPublish || ammoNow != _lastPublishedAmmo || magNow != _lastPublishedMag)
        {
            HUDWeaponBus.PublishAmmoChanged(ammoNow, magNow);
            _lastPublishedAmmo = ammoNow;
            _lastPublishedMag = magNow;
        }

        // Equip publish.
        if (!_hasInitialPublish || currentlyHeld != _lastPublishedHeld)
        {
            HUDWeaponBus.PublishEquipChanged(currentlyHeld);
            _lastPublishedHeld = currentlyHeld;
        }

        _hasInitialPublish = true;
    }

    private void PublishStatsForCurrent()
    {
        if (_currentManager == null) return;

        int maxRarityRoll = Mathf.Max(
            _currentManager.generatedPackage.slideRarity,
            _currentManager.generatedPackage.gripRarity);

        int magCapacity = _currentWeapon != null ? _currentWeapon.GetMaxBulletCount() : 0;

        HUDWeaponBus.PublishStatsChanged(new WeaponStatsSnapshot(
            maxRarityRoll,
            _currentManager.weaponDamageScale,
            magCapacity,
            _currentManager.recoilIntensityScale,
            _currentManager.projectileVelocityScale));
    }

    private static RaycastWeapon FindHeldWeapon()
    {
        // FindObjectsByType is OK at this scale (a handful of weapons in scene).
        // Could cache + refresh on grab events later if perf becomes an issue.
        RaycastWeapon[] weapons = FindObjectsByType<RaycastWeapon>(FindObjectsSortMode.None);
        for (int i = 0; i < weapons.Length; i++)
        {
            Grabbable g = weapons[i].GetComponent<Grabbable>();
            if (g != null && g.BeingHeld) return weapons[i];
        }
        return null;
    }
}
