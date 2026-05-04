using JerryScripts.Foundation.Damage;
using JerryScripts.Feature.WeaponHandling;
using UnityEngine;

namespace JerryScripts.Feature.WeaponGeneration
{
    /// <summary>
    /// ScriptableObject containing all designer-tunable parameters for the
    /// Weapon Generation system.
    ///
    /// <para><b>Per-rarity stat bands</b> define the randomisation range for the
    /// six rolled stats (base_damage, rounds_per_minute, mag_capacity,
    /// recoil_pitch_base, recoil_yaw_spread, bullet_speed).
    /// <c>max_range</c> is intentionally absent — it stays at the
    /// <c>WeaponData</c> SO default (50 m) and is never randomised
    /// (weapon-generation.md §Tuning Knobs).</para>
    ///
    /// <para><b>Shared barrel-guard pool</b> is rarity-agnostic: any entry may
    /// appear at any rarity tier. Pool size for alpha: 5 entries
    /// (Barrel-Guard-K through O). Selection is uniform
    /// (weapon-generation.md Rule 12).</para>
    /// </summary>
    /// <remarks>
    /// S2-009. GDD: weapon-generation.md §Tuning Knobs, §Mesh selection rules 9–12.
    /// Create via: Assets > Create > JerryScripts > Weapon Generation Config
    /// </remarks>
    [CreateAssetMenu(
        fileName = "WeaponGenerationConfig",
        menuName  = "JerryScripts/Weapon Generation Config",
        order     = 2)]
    public sealed class WeaponGenerationConfig : ScriptableObject
    {
        // ===================================================================
        // Nested type — per-rarity stat band
        // ===================================================================

        /// <summary>
        /// Min/max bands for all six randomised stats at a single rarity tier.
        /// Int fields (mag_capacity) use inclusive upper bound — the generator
        /// adds +1 to the Unity int-range exclusive-max per GDD Rule 4.
        /// </summary>
        [System.Serializable]
        public sealed class RarityStatBand
        {
            [Header("Damage")]
            [Tooltip("base_damage min (inclusive). GDD: Basic 18, Rare 26, Epic 36, Legendary 48.")]
            [Min(1f)]
            public float BaseDamageMin = 18f;

            [Tooltip("base_damage max (inclusive). GDD: Basic 22, Rare 32, Epic 44, Legendary 58.")]
            [Min(1f)]
            public float BaseDamageMax = 22f;

            [Header("Fire Rate")]
            [Tooltip("rounds_per_minute min (inclusive). GDD: Basic 150, Rare 180, Epic 200, Legendary 240.")]
            [Range(60f, 900f)]
            public float RoundsPerMinuteMin = 150f;

            [Tooltip("rounds_per_minute max (inclusive). GDD: Basic 200, Rare 240, Epic 280, Legendary 330.")]
            [Range(60f, 900f)]
            public float RoundsPerMinuteMax = 200f;

            [Header("Magazine")]
            [Tooltip("mag_capacity min (inclusive). GDD: Basic 10, Rare 12, Epic 14, Legendary 16.")]
            [Range(1, 30)]
            public int MagCapacityMin = 10;

            [Tooltip("mag_capacity max (inclusive). GDD: Basic 12, Rare 15, Epic 17, Legendary 20.")]
            [Range(1, 30)]
            public int MagCapacityMax = 12;

            [Header("Recoil")]
            [Tooltip("recoil_pitch_base min (deg, inclusive). Lower = less kick. GDD: Basic 5.0, Rare 4.0, Epic 3.0, Legendary 2.5.")]
            [Range(1f, 15f)]
            public float RecoilPitchBaseMin = 5.0f;

            [Tooltip("recoil_pitch_base max (deg, inclusive). GDD: Basic 6.5, Rare 5.5, Epic 4.5, Legendary 4.0.")]
            [Range(1f, 15f)]
            public float RecoilPitchBaseMax = 6.5f;

            [Tooltip("recoil_yaw_spread min (deg, inclusive). GDD: Basic 1.5, Rare 1.2, Epic 0.8, Legendary 0.5.")]
            [Range(0f, 5f)]
            public float RecoilYawSpreadMin = 1.5f;

            [Tooltip("recoil_yaw_spread max (deg, inclusive). GDD: Basic 2.5, Rare 2.0, Epic 1.5, Legendary 1.2.")]
            [Range(0f, 5f)]
            public float RecoilYawSpreadMax = 2.5f;

            [Header("Bullet Speed")]
            [Tooltip("bullet_speed min (m/s, inclusive). Data-only at alpha. GDD: Basic 80, Rare 100, Epic 130, Legendary 170.")]
            [Min(1f)]
            public float BulletSpeedMin = 80f;

            [Tooltip("bullet_speed max (m/s, inclusive). GDD: Basic 100, Rare 130, Epic 170, Legendary 230.")]
            [Min(1f)]
            public float BulletSpeedMax = 100f;
        }

        // ===================================================================
        // Inspector fields — per-rarity bands
        // ===================================================================

        [Header("Stat Bands — Per Rarity")]
        [Tooltip("Stat bands for Basic-rarity weapons. GDD defaults: DMG 18–22, RPM 150–200, MAG 10–12, PITCH 5.0–6.5, YAW 1.5–2.5, VEL 80–100.")]
        [SerializeField] private RarityStatBand _basicBand = new RarityStatBand
        {
            BaseDamageMin       = 18f,  BaseDamageMax       = 22f,
            RoundsPerMinuteMin  = 150f, RoundsPerMinuteMax  = 200f,
            MagCapacityMin      = 10,   MagCapacityMax      = 12,
            RecoilPitchBaseMin  = 5.0f, RecoilPitchBaseMax  = 6.5f,
            RecoilYawSpreadMin  = 1.5f, RecoilYawSpreadMax  = 2.5f,
            BulletSpeedMin      = 80f,  BulletSpeedMax      = 100f
        };

        [Tooltip("Stat bands for Rare-rarity weapons. GDD defaults: DMG 26–32, RPM 180–240, MAG 12–15, PITCH 4.0–5.5, YAW 1.2–2.0, VEL 100–130.")]
        [SerializeField] private RarityStatBand _rareBand = new RarityStatBand
        {
            BaseDamageMin       = 26f,  BaseDamageMax       = 32f,
            RoundsPerMinuteMin  = 180f, RoundsPerMinuteMax  = 240f,
            MagCapacityMin      = 12,   MagCapacityMax      = 15,
            RecoilPitchBaseMin  = 4.0f, RecoilPitchBaseMax  = 5.5f,
            RecoilYawSpreadMin  = 1.2f, RecoilYawSpreadMax  = 2.0f,
            BulletSpeedMin      = 100f, BulletSpeedMax      = 130f
        };

        [Tooltip("Stat bands for Epic-rarity weapons. GDD defaults: DMG 36–44, RPM 200–280, MAG 14–17, PITCH 3.0–4.5, YAW 0.8–1.5, VEL 130–170.")]
        [SerializeField] private RarityStatBand _epicBand = new RarityStatBand
        {
            BaseDamageMin       = 36f,  BaseDamageMax       = 44f,
            RoundsPerMinuteMin  = 200f, RoundsPerMinuteMax  = 280f,
            MagCapacityMin      = 14,   MagCapacityMax      = 17,
            RecoilPitchBaseMin  = 3.0f, RecoilPitchBaseMax  = 4.5f,
            RecoilYawSpreadMin  = 0.8f, RecoilYawSpreadMax  = 1.5f,
            BulletSpeedMin      = 130f, BulletSpeedMax      = 170f
        };

        [Tooltip("Stat bands for Legendary-rarity weapons. GDD defaults: DMG 48–58, RPM 240–330, MAG 16–20, PITCH 2.5–4.0, YAW 0.5–1.2, VEL 170–230.")]
        [SerializeField] private RarityStatBand _legendaryBand = new RarityStatBand
        {
            BaseDamageMin       = 48f,  BaseDamageMax       = 58f,
            RoundsPerMinuteMin  = 240f, RoundsPerMinuteMax  = 330f,
            MagCapacityMin      = 16,   MagCapacityMax      = 20,
            RecoilPitchBaseMin  = 2.5f, RecoilPitchBaseMax  = 4.0f,
            RecoilYawSpreadMin  = 0.5f, RecoilYawSpreadMax  = 1.2f,
            BulletSpeedMin      = 170f, BulletSpeedMax      = 230f
        };

        // ===================================================================
        // Inspector fields — mesh pool
        // ===================================================================

        [Header("Shared Barrel-Guard Pool (rarity-agnostic)")]
        [Tooltip(
            "The single shared pool of BarrelGuardData SOs used for all rarity tiers. " +
            "Alpha target: 5 entries (Barrel-Guard-K through O). " +
            "Selection is uniform (weapon-generation.md Rule 12). " +
            "If empty at runtime, WeaponGenerator instantiates a magenta debug cube " +
            "and logs an error (weapon-generation.md Rule 12).")]
        [SerializeField] private BarrelGuardData[] _barrelGuardPool = System.Array.Empty<BarrelGuardData>();

        [Header("Lower Receiver")]
        [Tooltip(
            "Universal lower receiver prefab (sourced from Pistol_M). " +
            "BarrelGuardMountPoint must be a child transform of this prefab. " +
            "Instantiated first at weapon spawn; barrel-guard is parented to its mount.")]
        [SerializeField] private GameObject _lowerReceiverPrefab;

        // ===================================================================
        // Public read-only API
        // ===================================================================

        /// <summary>
        /// Returns the <see cref="RarityStatBand"/> for the requested rarity.
        /// Never null — falls back to <c>_basicBand</c> for any unrecognised value.
        /// </summary>
        public RarityStatBand GetBand(WeaponRarity rarity)
        {
            return rarity switch
            {
                WeaponRarity.Basic     => _basicBand,
                WeaponRarity.Rare      => _rareBand,
                WeaponRarity.Epic      => _epicBand,
                WeaponRarity.Legendary => _legendaryBand,
                _                      => _basicBand
            };
        }

        /// <summary>
        /// Shared rarity-agnostic barrel-guard pool.
        /// May be empty — callers must handle the empty-pool fallback per GDD Rule 12.
        /// </summary>
        public BarrelGuardData[] BarrelGuardPool => _barrelGuardPool;

        /// <summary>
        /// Universal lower receiver prefab. May be null — callers must null-check.
        /// </summary>
        public GameObject LowerReceiverPrefab => _lowerReceiverPrefab;
    }
}
