using BNG;
using JerryScripts.Core.PlayerState;
using JerryScripts.Foundation.Player;
using UnityEngine;

/// <summary>
/// Singleton bridge between BNG/Sam's weapon side and Jerry's HUD. Lives on the
/// <c>_Systems</c> GameObject (one per scene). Each frame:
/// <list type="bullet">
///   <item>Finds whichever <c>RaycastWeapon</c> in the scene is currently held
///         (sibling <c>Grabbable.BeingHeld</c> is true)</item>
///   <item>Reads its ammo + max + <c>GeneratedWeaponManager</c> stats</item>
///   <item>Publishes to <see cref="HUDWeaponBus"/></item>
///   <item>Disables <c>RaycastWeapon.enabled</c> while the player is Paused or Dead
///         so the trigger can't fire during/across a pause</item>
/// </list>
///
/// <para>Stats republish on weapon switch AND when <c>GetMaxBulletCount()</c> changes
/// (e.g. player inserts a magazine into an empty pistol — the HUD-06 MAG bar updates
/// without needing to drop+pickup).</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class BNGWeaponBridge : MonoBehaviour
{
    private RaycastWeapon _currentWeapon;
    private GeneratedWeaponManager _currentManager;
    private PlayerStateManager _psm;

    private int _lastPublishedAmmo = -1;
    private int _lastPublishedMag = -1;
    private bool _lastPublishedHeld;
    private bool _hasInitialPublish;
    private int _lastSnapshotMag = -1;
    private bool _lastSetWeaponEnabled = true;

    private void Awake()
    {
        _psm = FindAnyObjectByType<PlayerStateManager>();
    }

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

        // Ammo + max polling.
        int ammoNow = currentlyHeld ? _currentWeapon.GetBulletCount() : 0;
        int magNow  = currentlyHeld ? _currentWeapon.GetMaxBulletCount() : 0;

        // Mag-capacity change detection (e.g. player inserts a magazine after grabbing
        // an empty pistol). Republish stats so the HUD-06 MAG bar updates immediately
        // without needing a drop+pickup cycle.
        if (currentlyHeld && magNow != _lastSnapshotMag)
        {
            _lastSnapshotMag = magNow;
            PublishStatsForCurrent();
        }

        if (!_hasInitialPublish || ammoNow != _lastPublishedAmmo || magNow != _lastPublishedMag)
        {
            HUDWeaponBus.PublishAmmoChanged(ammoNow, magNow);
            _lastPublishedAmmo = ammoNow;
            _lastPublishedMag = magNow;
        }

        if (!_hasInitialPublish || currentlyHeld != _lastPublishedHeld)
        {
            HUDWeaponBus.PublishEquipChanged(currentlyHeld);
            _lastPublishedHeld = currentlyHeld;
        }

        // Pause/Death gate: disable RaycastWeapon when world is frozen so the trigger
        // can't fire across a pause boundary. Re-enable on resume (state back to Running).
        SyncWeaponEnabledToPlayerState(currentlyHeld);

        _hasInitialPublish = true;
    }

    private void SyncWeaponEnabledToPlayerState(bool currentlyHeld)
    {
        if (!currentlyHeld) return;

        // Resolve PSM lazily in case Awake order missed it
        if (_psm == null) _psm = FindAnyObjectByType<PlayerStateManager>();
        bool worldRunning = _psm == null || _psm.CurrentState == PlayerState.Running;

        if (_currentWeapon.enabled != worldRunning)
        {
            _currentWeapon.enabled = worldRunning;
            _lastSetWeaponEnabled = worldRunning;
        }
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
        RaycastWeapon[] weapons = FindObjectsByType<RaycastWeapon>(FindObjectsSortMode.None);
        for (int i = 0; i < weapons.Length; i++)
        {
            Grabbable g = weapons[i].GetComponent<Grabbable>();
            if (g != null && g.BeingHeld) return weapons[i];
        }
        return null;
    }
}
