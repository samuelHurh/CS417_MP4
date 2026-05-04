using UnityEngine;

namespace JerryScripts.Foundation.Damage
{
    /// <summary>
    /// MonoBehaviour implementation of <see cref="IDamageResolver"/>.
    /// Place one instance in the scene; wire the <see cref="RarityMultiplierTable"/> SO
    /// in the Inspector. All other systems consume this via the <see cref="IDamageResolver"/>
    /// interface — never hold a direct reference to this concrete class.
    /// </summary>
    /// <remarks>
    /// This component holds no mutable state beyond the SO reference. It is logically
    /// stateless: the same inputs always produce the same output.
    /// GDD: damage-system.md §Formulas + §Edge Cases.
    /// Architecture: architecture.md §3.7 (DamageResolver.Awake loads rarity multiplier SO).
    /// </remarks>
    public sealed class DamageResolver : MonoBehaviour, IDamageResolver
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Data")]
        [Tooltip(
            "ScriptableObject that owns all rarity multipliers, the player damage cap, " +
            "the enemy damage floor, and the enemy damage scalar. Must be assigned.")]
        [SerializeField] private RarityMultiplierTable _rarityTable;

        // ===================================================================
        // Unity lifecycle
        // ===================================================================

        private void Awake()
        {
            if (_rarityTable == null)
            {
                Debug.LogError(
                    "[DamageResolver] _rarityTable is not assigned. " +
                    "Assign a RarityMultiplierTable ScriptableObject in the Inspector. " +
                    "Damage resolution will return floor values until this is fixed.",
                    this);
            }
        }

        // ===================================================================
        // IDamageResolver implementation
        // ===================================================================

        /// <inheritdoc/>
        /// <remarks>
        /// Formula (GDD): <c>final_damage = clamp(baseDamage * rarityMultiplier, 1.0, playerDamageCap)</c>.
        /// </remarks>
        public DamageEvent ResolvePlayerDamage(
            float baseDamage,
            float rarityMultiplier,
            Vector3 hitPos,
            string sourceId)
        {
            float floor = SafeFloor();
            float cap   = SafeCap();

            // --- Edge case: NaN / Infinity (GDD §Edge Cases) ---
            if (float.IsNaN(baseDamage) || float.IsInfinity(baseDamage) ||
                float.IsNaN(rarityMultiplier) || float.IsInfinity(rarityMultiplier))
            {
                Debug.LogError(
                    $"[DamageResolver] ResolvePlayerDamage received invalid input " +
                    $"(baseDamage={baseDamage}, rarityMultiplier={rarityMultiplier}). " +
                    $"Rejecting event and returning floor ({floor}). Fix the caller.",
                    this);
                return new DamageEvent(floor, sourceId ?? string.Empty, true, hitPos);
            }

            // --- Edge case: non-positive inputs → floor + warning (GDD Core Rule 5 + §Edge Cases) ---
            if (baseDamage <= 0f || rarityMultiplier <= 0f)
            {
                Debug.LogWarning(
                    $"[DamageResolver] ResolvePlayerDamage: baseDamage ({baseDamage}) or " +
                    $"rarityMultiplier ({rarityMultiplier}) is <= 0. " +
                    $"Clamping to floor ({floor}). Silent zero damage is a bug.",
                    this);
                return new DamageEvent(floor, sourceId ?? string.Empty, true, hitPos);
            }

            // --- Happy path ---
            float raw    = baseDamage * rarityMultiplier;
            float clamped = Mathf.Clamp(raw, floor, cap);

            return new DamageEvent(clamped, sourceId ?? string.Empty, true, hitPos);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Formula (GDD): <c>final_damage = max(enemyBaseDamage * enemyDamageScalar, enemyDamageFloor)</c>.
        /// </remarks>
        public DamageEvent ResolveEnemyDamage(
            float enemyBaseDamage,
            Vector3 hitPos,
            string enemyId)
        {
            float floor  = SafeFloor();
            float scalar = SafeScalar();

            // --- Edge case: NaN / Infinity (GDD §Edge Cases) ---
            if (float.IsNaN(enemyBaseDamage) || float.IsInfinity(enemyBaseDamage))
            {
                Debug.LogError(
                    $"[DamageResolver] ResolveEnemyDamage received invalid input " +
                    $"(enemyBaseDamage={enemyBaseDamage}). " +
                    $"Rejecting event and returning floor ({floor}). Fix the caller.",
                    this);
                return new DamageEvent(floor, enemyId ?? string.Empty, false, hitPos);
            }

            // --- Happy path: max(scaled, floor) ---
            float scaled = enemyBaseDamage * scalar;
            float final  = Mathf.Max(scaled, floor);

            return new DamageEvent(final, enemyId ?? string.Empty, false, hitPos);
        }

        // ===================================================================
        // Rarity lookup helper (consumed by ProjectileSystem / WeaponHandling)
        // ===================================================================

        /// <summary>
        /// Returns the rarity multiplier for the given <see cref="WeaponRarity"/> tier.
        /// Thin delegating wrapper around <see cref="RarityMultiplierTable.MultiplierFor"/>
        /// so callers that already hold a <c>DamageResolver</c> reference don't need
        /// a second reference to the SO.
        /// </summary>
        /// <remarks>
        /// Returns 1.0 (Basic tier) if the rarity table is not assigned.
        /// (Historical note: this used to live on the concrete class to avoid pulling
        /// <see cref="WeaponRarity"/> into <see cref="IDamageResolver"/> when the enum
        /// was in the Feature layer. The enum now lives in <c>Foundation.Damage</c>,
        /// so adding to the interface would be safe — kept on the concrete class for
        /// API stability.)
        /// </remarks>
        public float MultiplierFor(WeaponRarity rarity)
        {
            return _rarityTable != null ? _rarityTable.MultiplierFor(rarity) : 1.0f;
        }

        // ===================================================================
        // Private helpers — null-safe SO access
        // ===================================================================

        /// <summary>
        /// Returns <see cref="RarityMultiplierTable.EnemyDamageFloor"/> or 1.0 if the SO is missing.
        /// </summary>
        private float SafeFloor()  => _rarityTable != null ? _rarityTable.EnemyDamageFloor  : 1.0f;

        /// <summary>
        /// Returns <see cref="RarityMultiplierTable.PlayerDamageCap"/> or 330.0 if the SO is missing.
        /// </summary>
        private float SafeCap()    => _rarityTable != null ? _rarityTable.PlayerDamageCap   : 330.0f;

        /// <summary>
        /// Returns <see cref="RarityMultiplierTable.EnemyDamageScalar"/> or 1.0 if the SO is missing.
        /// </summary>
        private float SafeScalar() => _rarityTable != null ? _rarityTable.EnemyDamageScalar : 1.0f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ===================================================================
        // Test-only injection surface (EditMode tests only)
        // ===================================================================

        /// <summary>
        /// Injects a <see cref="RarityMultiplierTable"/> instance for unit testing.
        /// Available in Editor and development builds only.
        /// Do NOT call from production code.
        /// </summary>
        /// <param name="table">The table instance to use. Must not be null.</param>
        internal void InjectRarityTable(RarityMultiplierTable table)
        {
            _rarityTable = table;
        }
#endif
    }
}
