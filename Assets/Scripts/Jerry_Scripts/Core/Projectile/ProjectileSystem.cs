using JerryScripts.Feature.WeaponHandling;
using JerryScripts.Foundation.Damage;
using UnityEngine;

namespace JerryScripts.Core.Projectile
{
    /// <summary>
    /// MonoBehaviour implementation of <see cref="IProjectileService"/>.
    /// Place one instance on a <c>_Systems</c> GameObject alongside
    /// <see cref="DamageResolver"/>.
    ///
    /// <para><b>Scope (Jerry):</b> hitscan only. Enemy projectile spawning is Sam's
    /// Enemy System scope and is intentionally not implemented here.</para>
    ///
    /// <para><b>Hitscan path:</b> Fires a single non-alloc <c>Physics.Raycast</c> on the
    /// <c>EnemyHitbox</c> layer. Damage is resolved by reading the rarity multiplier from
    /// <see cref="DamageResolver.MultiplierFor"/> (a concrete helper, not on the
    /// <see cref="IDamageResolver"/> interface) so the interface contract stays unchanged.
    /// The multiplier lookup is a one-line SO field read — zero allocations.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileSystem : MonoBehaviour, IProjectileService
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("References")]
        [Tooltip(
            "Concrete DamageResolver in the scene. Auto-resolved in Awake if left empty. " +
            "Drag the _Systems/DamageResolver component here for deterministic wiring.")]
        [SerializeField] private DamageResolver _damageResolver;

        [Header("Hitscan")]
        [Tooltip(
            "LayerMask targeting the EnemyHitbox layer. Configure in Project Settings > Tags & Layers. " +
            "Set this mask to the EnemyHitbox layer bit in the Inspector.")]
        [SerializeField] private LayerMask _enemyHitboxMask;

        [Tooltip(
            "Fallback maximum hitscan range used when WeaponData.MaxRange is 0 or negative. " +
            "GDD default: 100 m. Safe range: 10–200 m.")]
        [Min(1f)]
        [SerializeField] private float _defaultHitscanRange = 100f;

        // ===================================================================
        // Private — cached state
        // ===================================================================

        /// <summary>
        /// Single reused RaycastHit struct. The 'out' parameter pattern reuses the
        /// stack allocation — no heap hit.
        /// </summary>
        private RaycastHit _hitResult;

        // ===================================================================
        // Unity lifecycle
        // ===================================================================

        private void Awake()
        {
            // Auto-resolve DamageResolver if not inspector-wired
            if (_damageResolver == null)
                _damageResolver = FindAnyObjectByType<DamageResolver>();

            ValidateReferences();
        }

        // ===================================================================
        // IProjectileService implementation
        // ===================================================================

        /// <inheritdoc/>
        /// <remarks>
        /// Zero GC allocations: single <c>Physics.Raycast</c> overload with <c>out RaycastHit</c>
        /// (non-alloc, stack-allocated hit result). No string building in the hot path.
        /// GDD §Core Rule 1, §Core Rule 5 (single target, no pierce).
        /// </remarks>
        public bool FireHitscan(Transform muzzle, WeaponData weaponData)
        {
            if (muzzle == null || weaponData == null) return false;

            float range = weaponData.MaxRange > 0f ? weaponData.MaxRange : _defaultHitscanRange;

            // Non-alloc single-hit raycast (architecture §4.5, physics.md §Raycasting)
            bool hit = Physics.Raycast(
                muzzle.position,
                muzzle.forward,
                out _hitResult,
                range,
                _enemyHitboxMask);

            if (!hit) return false;

            // --- Damage resolution ---
            // Read the rarity multiplier via the concrete DamageResolver helper.
            float multiplier = _damageResolver != null
                ? _damageResolver.MultiplierFor(weaponData.Rarity)
                : 1f;

            DamageEvent dmg = _damageResolver != null
                ? _damageResolver.ResolvePlayerDamage(
                    weaponData.BaseDamage,
                    multiplier,
                    _hitResult.point,
                    weaponData.WeaponName)
                : new DamageEvent(weaponData.BaseDamage, weaponData.WeaponName, true, _hitResult.point);

            // --- Deliver damage (null-safe) ---
            IHittable hittable = _hitResult.collider.GetComponentInParent<IHittable>();
            hittable?.TakeDamage(in dmg);

            // --- Hit confirmation audio stub (S1-006) ---
            // TODO (S1-006): IAudioFeedbackService.PostFeedbackEvent(HitConfirmation, hitPos, finalDamage)
            Debug.Log(
                $"[ProjectileSystem] Hitscan hit '{_hitResult.collider.name}'. " +
                $"FinalDamage={dmg.FinalDamage:F1} at {_hitResult.point}. " +
                "PostFeedbackEvent(HitConfirmation) stub — wire AudioFeedbackService in S1-006.",
                this);

            return true;
        }

        // ===================================================================
        // Validation
        // ===================================================================

        private void ValidateReferences()
        {
            if (_damageResolver == null)
                Debug.LogWarning(
                    "[ProjectileSystem] DamageResolver not found. Hitscan hits will return " +
                    "base damage without rarity scaling. Add a DamageResolver to the scene.",
                    this);

            if (_enemyHitboxMask.value == 0)
                Debug.LogWarning(
                    "[ProjectileSystem] _enemyHitboxMask is zero (no layers selected). " +
                    "Hitscan will never hit anything. Set the mask to the EnemyHitbox layer " +
                    "in the Inspector (Project Settings > Tags & Layers > EnemyHitbox).",
                    this);
        }
    }
}
