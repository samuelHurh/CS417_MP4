using UnityEngine;

namespace JerryScripts.Feature.WeaponHandling
{
    /// <summary>
    /// Data-only ScriptableObject that describes a single weapon archetype.
    /// Weapon Generation produces instances of this asset; WeaponInstance reads from it.
    /// All tunable constants come from here — nothing is hardcoded in MonoBehaviours.
    /// </summary>
    /// <remarks>
    /// Source: GDD §4.4 (core-fps-weapon-handling.md), Tuning Knobs table.
    /// Create via: Assets > Create > JerryScripts > Weapon Data
    /// </remarks>
    [CreateAssetMenu(
        fileName = "NewWeaponData",
        menuName = "JerryScripts/Weapon Data",
        order = 0)]
    public sealed class WeaponData : ScriptableObject
    {
        // ===================================================================
        // Identity
        // ===================================================================

        [Header("Identity")]
        [Tooltip("Human-readable weapon name displayed in diegetic UI.")]
        [SerializeField] private string _weaponName = "Pistol";

        [Tooltip("Weapon category tag. Used by Inventory System for slot routing.")]
        [SerializeField] private WeaponType _weaponType = WeaponType.Pistol;

        [Tooltip("Rarity tier. Feeds into damage formula as rarity_multiplier.")]
        [SerializeField] private WeaponRarity _rarity = WeaponRarity.Basic;

        // ===================================================================
        // Combat
        // ===================================================================

        [Header("Combat")]
        [Tooltip("Base damage per hitscan hit. Feeds into Damage System formula.")]
        [Min(1f)]
        [SerializeField] private float _baseDamage = 20f;

        [Tooltip(
            "Fire rate in rounds per minute. " +
            "fire_interval = 60 / rounds_per_minute. " +
            "GDD range: 60–900. Floor-clamped to 0.05s interval.")]
        [Range(60f, 900f)]
        [SerializeField] private float _roundsPerMinute = 180f;

        [Tooltip("Maximum hitscan range in metres.")]
        [Min(0.1f)]
        [SerializeField] private float _maxRange = 50f;

        // ===================================================================
        // Magazine / Ammo
        // ===================================================================

        [Header("Magazine")]
        [Tooltip("Maximum rounds per magazine. GDD range: 1–30.")]
        [Range(1, 30)]
        [SerializeField] private int _magCapacity = 12;

        [Tooltip(
            "Seconds the dropped (ejected) magazine persists before being destroyed. " +
            "GDD default: 8.0 s, range: 3–20 s.")]
        [Range(3f, 20f)]
        [SerializeField] private float _magazinePersistSeconds = 8f;

        [Tooltip(
            "Seconds after mag-drop before the fresh magazine spawns on the offhand. " +
            "GDD default: 0.2 s, range: 0–0.5 s.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _magSpawnDelay = 0.2f;

        // ===================================================================
        // Interaction Radii
        // ===================================================================

        [Header("Interaction Radii")]
        [Tooltip(
            "Sphere radius within which the player's hand triggers a grab highlight and can grab. " +
            "GDD default: 0.15 m, range: 0.05–0.30 m.")]
        [Range(0.05f, 0.30f)]
        [SerializeField] private float _grabRadius = 0.15f;

        [Tooltip(
            "Distance within which the weapon snaps to a rig mount point on drop. " +
            "GDD default: 0.15 m, range: 0.05–0.25 m.")]
        [Range(0.05f, 0.25f)]
        [SerializeField] private float _holsterSnapRadius = 0.15f;

        [Tooltip(
            "Distance within which the offhand magazine snaps into the mag well. " +
            "GDD default: 0.05 m, range: 0.02–0.10 m.")]
        [Range(0.02f, 0.10f)]
        [SerializeField] private float _magInsertionRadius = 0.05f;

        // ===================================================================
        // Recoil
        // ===================================================================

        [Header("Recoil")]
        [Tooltip(
            "Upward pitch rotation per shot in degrees. " +
            "GDD default: 4.0 deg, range: 1–15 deg. " +
            "Applied after hitscan is sampled — purely cosmetic.")]
        [Range(1f, 15f)]
        [SerializeField] private float _recoilPitchBase = 4f;

        [Tooltip(
            "Half-angle of lateral yaw spread in degrees. " +
            "Actual yaw is Random.Range(-spread, +spread) per shot. " +
            "GDD default: 1.5 deg, range: 0–5 deg.")]
        [Range(0f, 5f)]
        [SerializeField] private float _recoilYawSpread = 1.5f;

        [Tooltip(
            "Seconds for recoil rotation to decay back to zero via linear interpolation. " +
            "GDD default: 0.18 s, range: 0.05–0.5 s.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _recoilRecoveryTime = 0.18f;

        // ===================================================================
        // Haptics
        // ===================================================================

        [Header("Haptics")]
        [Tooltip(
            "Amplitude for haptic impulse on a valid shot. " +
            "GDD default: 0.8, range: 0–1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _hapticFireAmplitude = 0.8f;

        [Tooltip(
            "Duration for haptic impulse on a valid shot in seconds. " +
            "GDD default: 0.04 s, range: 0.02–0.15 s.")]
        [Range(0.02f, 0.15f)]
        [SerializeField] private float _hapticFireDuration = 0.04f;

        [Tooltip(
            "Amplitude for haptic impulse on a dry-fire (empty magazine). " +
            "GDD default: 0.2, range: 0–0.5.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _hapticDryFireAmplitude = 0.2f;

        // ===================================================================
        // Prefab References
        // ===================================================================

        [Header("Prefabs")]
        [Tooltip("Prefab instantiated as the ejected magazine during reload.")]
        [SerializeField] private GameObject _magazinePrefab;

        [Tooltip("Particle system prefab for the muzzle flash. Played at the muzzle transform.")]
        [SerializeField] private GameObject _muzzleFlashPrefab;

        // ===================================================================
        // Public read-only API
        // ===================================================================

        /// <summary>Human-readable weapon name.</summary>
        public string WeaponName => _weaponName;

        /// <summary>Category of weapon (pistol, rifle, etc.).</summary>
        public WeaponType WeaponType => _weaponType;

        /// <summary>Rarity tier — fed into damage formula as rarity_multiplier.</summary>
        public WeaponRarity Rarity => _rarity;

        /// <summary>Base damage per hitscan hit before any multipliers.</summary>
        public float BaseDamage => _baseDamage;

        /// <summary>
        /// Fire rate in rounds per minute.
        /// Use <see cref="FireInterval"/> for the derived per-shot minimum gap.
        /// </summary>
        public float RoundsPerMinute => _roundsPerMinute;

        /// <summary>
        /// Derived minimum seconds between shots: <c>60 / rounds_per_minute</c>,
        /// clamped to a 0.05s floor (prevents single-frame multi-fire).
        /// GDD formula: fire_interval = 60.0 / rounds_per_minute.
        /// </summary>
        public float FireInterval
        {
            get
            {
                if (_roundsPerMinute <= 0f)
                {
                    Debug.LogError(
                        $"[WeaponData] '{name}' has rounds_per_minute <= 0. " +
                        "Clamping fire_interval to 1.0s. Fix the asset.",
                        this);
                    return 1f;
                }

                return Mathf.Max(60f / _roundsPerMinute, 0.05f);
            }
        }

        /// <summary>Maximum hitscan distance in metres.</summary>
        public float MaxRange => _maxRange;

        /// <summary>Maximum rounds per loaded magazine.</summary>
        public int MagCapacity => _magCapacity;

        /// <summary>Seconds the ejected (dropped) magazine persists before destruction.</summary>
        public float MagazinePersistSeconds => _magazinePersistSeconds;

        /// <summary>Seconds after mag-drop before the fresh offhand magazine spawns.</summary>
        public float MagSpawnDelay => _magSpawnDelay;

        /// <summary>Hand proximity radius for grab highlighting and grab detection (metres).</summary>
        public float GrabRadius => _grabRadius;

        /// <summary>Drop proximity radius for snapping to a rig mount point (metres).</summary>
        public float HolsterSnapRadius => _holsterSnapRadius;

        /// <summary>Magazine proximity radius for mag-well snap-in (metres).</summary>
        public float MagInsertionRadius => _magInsertionRadius;

        /// <summary>Upward pitch rotation applied per shot (degrees). Purely cosmetic.</summary>
        public float RecoilPitchBase => _recoilPitchBase;

        /// <summary>Half-angle of lateral yaw spread per shot (degrees). Purely cosmetic.</summary>
        public float RecoilYawSpread => _recoilYawSpread;

        /// <summary>Seconds for recoil rotation to decay to zero.</summary>
        public float RecoilRecoveryTime => _recoilRecoveryTime;

        /// <summary>Haptic amplitude (0–1) sent on a valid shot.</summary>
        public float HapticFireAmplitude => _hapticFireAmplitude;

        /// <summary>Haptic duration (seconds) sent on a valid shot.</summary>
        public float HapticFireDuration => _hapticFireDuration;

        /// <summary>Haptic amplitude (0–1) sent on a dry-fire.</summary>
        public float HapticDryFireAmplitude => _hapticDryFireAmplitude;

        /// <summary>Ejected magazine prefab. May be null if no physical magazine prop is needed.</summary>
        public GameObject MagazinePrefab => _magazinePrefab;

        /// <summary>Muzzle flash particle prefab. May be null to skip VFX.</summary>
        public GameObject MuzzleFlashPrefab => _muzzleFlashPrefab;
    }

    // -----------------------------------------------------------------------
    // Supporting enums (same file — small, tightly coupled)
    // -----------------------------------------------------------------------

    /// <summary>Weapon category. Used by the Inventory System for slot routing.</summary>
    public enum WeaponType
    {
        Pistol,
        Rifle,
        Shotgun,
        SubmachineGun,
        SniperRifle
    }

    /// <summary>
    /// Rarity tier. Maps to a float multiplier in the Damage System formula.
    /// Multiplier values are owned by the Damage System (<c>RarityMultiplierTable</c>).
    /// </summary>
    /// <remarks>
    /// Matches GDD damage-system.md §Rarity Multiplier Table: four tiers only.
    /// Canonical multipliers: Basic 1.0, Rare 1.3, Epic 1.7, Legendary 2.2.
    /// </remarks>
    public enum WeaponRarity
    {
        Basic,
        Rare,
        Epic,
        Legendary
    }
}
