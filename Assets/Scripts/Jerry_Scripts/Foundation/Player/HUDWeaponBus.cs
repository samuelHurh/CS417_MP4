using System;

namespace JerryScripts.Foundation.Player
{
    /// <summary>
    /// Snapshot of the weapon's current generated stats. Published by a weapon-side
    /// bridge (currently <c>BNGWeaponBridge</c> in default Assembly-CSharp) and consumed
    /// by <c>HUDSystem</c>. Decouples Jerry's asmdef from BNG and Sam's GeneratedWeaponManager.
    /// </summary>
    public readonly struct WeaponStatsSnapshot
    {
        /// <summary>max(slideRarity, gripRarity) — drives HUD-06 rarity color.</summary>
        public int MaxRarityRoll { get; }

        /// <summary>Damage scale factor (typically 1.0). HUD-06 DMG bar.</summary>
        public float DamageScale { get; }

        /// <summary>Magazine capacity. HUD-06 MAG bar.</summary>
        public int MagazineSize { get; }

        /// <summary>Recoil intensity scale (lower = better — fuller HUD-06 REC bar).</summary>
        public float RecoilIntensityScale { get; }

        /// <summary>Projectile velocity scale. HUD-06 VEL bar.</summary>
        public float ProjectileVelocityScale { get; }

        public WeaponStatsSnapshot(
            int maxRarityRoll,
            float damageScale,
            int magazineSize,
            float recoilIntensityScale,
            float projectileVelocityScale)
        {
            MaxRarityRoll           = maxRarityRoll;
            DamageScale             = damageScale;
            MagazineSize            = magazineSize;
            RecoilIntensityScale    = recoilIntensityScale;
            ProjectileVelocityScale = projectileVelocityScale;
        }
    }

    /// <summary>
    /// Static event bus bridging the BNG/Sam side of weapon state to Jerry's <c>HUDSystem</c>.
    /// Published by a bridge MonoBehaviour in default Assembly-CSharp (e.g. <c>BNGWeaponBridge</c>);
    /// subscribed to by <c>HUDSystem</c> in Jerry's asmdef.
    ///
    /// <para><b>Why static</b>: there is exactly one HUD and exactly one weapon at any time
    /// in the alpha. Static events let the two sides communicate without crossing the asmdef
    /// boundary (Jerry's asmdef can't reference types in default Assembly-CSharp directly).
    /// If the alpha grows multi-weapon, replace this with a registry pattern.</para>
    /// </summary>
    public static class HUDWeaponBus
    {
        /// <summary>Fires when (current, max) ammo changes. Args: currentBullets, magCapacity.</summary>
        public static event Action<int, int> OnAmmoChanged;

        /// <summary>Fires when held state changes. Arg: true if currently held by a hand.</summary>
        public static event Action<bool> OnEquipChanged;

        /// <summary>Fires when the weapon's stat snapshot changes (typically once at weapon Start).</summary>
        public static event Action<WeaponStatsSnapshot> OnStatsChanged;

        // ==============================
        // Snapshot of last published values — HUDSystem reads these on initial sync
        // ==============================

        public static int  LastAmmoCurrent { get; private set; }
        public static int  LastAmmoMax     { get; private set; }
        public static bool LastIsHeld      { get; private set; }
        public static WeaponStatsSnapshot LastStats { get; private set; }
        public static bool HasPublishedStats { get; private set; }

        // ==============================
        // Publishers (called by bridges)
        // ==============================

        public static void PublishAmmoChanged(int current, int max)
        {
            LastAmmoCurrent = current;
            LastAmmoMax = max;
            OnAmmoChanged?.Invoke(current, max);
        }

        public static void PublishEquipChanged(bool isHeld)
        {
            LastIsHeld = isHeld;
            OnEquipChanged?.Invoke(isHeld);
        }

        public static void PublishStatsChanged(WeaponStatsSnapshot snapshot)
        {
            LastStats = snapshot;
            HasPublishedStats = true;
            OnStatsChanged?.Invoke(snapshot);
        }
    }
}
