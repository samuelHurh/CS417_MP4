using UnityEngine;
using JerryScripts.Feature.WeaponHandling;

namespace JerryScripts.Foundation.Damage
{
    /// <summary>
    /// ScriptableObject that owns all tunable constants for the Damage System.
    /// Runtime code reads these values; it must never write to them.
    /// Tuning happens by editing the asset in the Editor, not by code mutation.
    /// </summary>
    /// <remarks>
    /// Source: architecture.md §4.4 (RarityMultiplierTable SO contract).
    /// GDD: damage-system.md §Tuning Knobs and §Formulas / Rarity Multiplier Table.
    /// Create via: Assets > Create > CS417 > Foundation > Rarity Multiplier Table.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "RarityMultiplierTable",
        menuName  = "CS417/Foundation/Rarity Multiplier Table",
        order     = 10)]
    public sealed class RarityMultiplierTable : ScriptableObject
    {
        // ===================================================================
        // Rarity tier multipliers (GDD §Formulas / Rarity Multiplier Table)
        // ===================================================================

        [Header("Rarity Multipliers")]

        [Tooltip("Damage multiplier for Basic tier. GDD value: 1.0. Safe range: 1.0–1.1.")]
        [Range(1.0f, 1.1f)]
        [SerializeField] private float _basic = 1.0f;

        [Tooltip("Damage multiplier for Rare tier. GDD value: 1.3. Safe range: 1.2–1.5.")]
        [Range(1.2f, 1.5f)]
        [SerializeField] private float _rare = 1.3f;

        [Tooltip("Damage multiplier for Epic tier. GDD value: 1.7. Safe range: 1.5–2.0.")]
        [Range(1.5f, 2.0f)]
        [SerializeField] private float _epic = 1.7f;

        [Tooltip("Damage multiplier for Legendary tier. GDD value: 2.2. Safe range: 2.0–3.0.")]
        [Range(2.0f, 3.0f)]
        [SerializeField] private float _legendary = 2.2f;

        // ===================================================================
        // Global caps and floors
        // ===================================================================

        [Header("Caps and Floors")]

        [Tooltip(
            "Upper clamp on player weapon final_damage. Prevents one-shot boss kills. " +
            "GDD default: 330.0. Safe range: 100–999.")]
        [Range(100f, 999f)]
        [SerializeField] private float _playerDamageCap = 330.0f;

        [Tooltip(
            "Minimum damage any source (player or enemy) can deal. " +
            "Silent zero damage is a bug — the floor prevents it. " +
            "GDD default: 1.0. Safe range: 1.0–5.0.")]
        [Range(1.0f, 5.0f)]
        [SerializeField] private float _enemyDamageFloor = 1.0f;

        [Tooltip(
            "Global difficulty scalar applied to all enemy base damage values. " +
            "GDD default: 1.0. Safe range: 0.5–2.0.")]
        [Range(0.5f, 2.0f)]
        [SerializeField] private float _enemyDamageScalar = 1.0f;

        // ===================================================================
        // Public read-only API
        // ===================================================================

        /// <summary>Damage multiplier for the Basic rarity tier. GDD canonical value: 1.0.</summary>
        public float Basic => _basic;

        /// <summary>Damage multiplier for the Rare rarity tier. GDD canonical value: 1.3.</summary>
        public float Rare => _rare;

        /// <summary>Damage multiplier for the Epic rarity tier. GDD canonical value: 1.7.</summary>
        public float Epic => _epic;

        /// <summary>Damage multiplier for the Legendary rarity tier. GDD canonical value: 2.2.</summary>
        public float Legendary => _legendary;

        /// <summary>
        /// Upper clamp for player weapon <c>final_damage</c>.
        /// Prevents single-shot kills on tanky enemies/bosses. GDD default: 330.0.
        /// </summary>
        public float PlayerDamageCap => _playerDamageCap;

        /// <summary>
        /// Minimum damage any source can deal. Applied as a floor after all formula
        /// calculations. GDD default: 1.0.
        /// </summary>
        public float EnemyDamageFloor => _enemyDamageFloor;

        /// <summary>
        /// Global difficulty tuning scalar applied to all enemy base damage values.
        /// GDD default: 1.0 (no scaling). Range 0.5 (easier) to 2.0 (harder).
        /// </summary>
        public float EnemyDamageScalar => _enemyDamageScalar;

        // ===================================================================
        // Rarity enum lookup
        // ===================================================================

        /// <summary>
        /// Returns the float multiplier for the given <see cref="WeaponRarity"/> value.
        /// </summary>
        /// <remarks>
        /// Mapping from the 4-value <see cref="WeaponRarity"/> enum to the GDD's 4-tier table:
        /// <list type="bullet">
        ///   <item><description><c>Basic</c>     → 1.0</description></item>
        ///   <item><description><c>Rare</c>      → 1.3</description></item>
        ///   <item><description><c>Epic</c>      → 1.7</description></item>
        ///   <item><description><c>Legendary</c> → 2.2</description></item>
        /// </list>
        /// If an unknown enum value is encountered, returns <c>Basic</c> (1.0) and logs an error.
        /// </remarks>
        /// <param name="rarity">The weapon's rarity tier.</param>
        /// <returns>The float multiplier to use in the damage formula.</returns>
        public float MultiplierFor(WeaponRarity rarity)
        {
            switch (rarity)
            {
                case WeaponRarity.Basic:     return _basic;
                case WeaponRarity.Rare:      return _rare;
                case WeaponRarity.Epic:      return _epic;
                case WeaponRarity.Legendary: return _legendary;
                default:
                    Debug.LogError(
                        $"[RarityMultiplierTable] Unknown WeaponRarity value '{rarity}'. " +
                        "Defaulting to Basic (1.0). Add the new tier to MultiplierFor().",
                        this);
                    return _basic;
            }
        }
    }
}
