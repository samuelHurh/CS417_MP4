using UnityEngine;

namespace JerryScripts.Feature.WeaponHandling
{
    /// <summary>
    /// Round-robin pool of pre-warmed <see cref="ParticleSystem"/> instances for muzzle flash VFX.
    ///
    /// <para><b>Placement:</b> add as a component on the same <c>_Systems</c> GameObject that hosts
    /// <c>AudioFeedbackService</c> and <c>ProjectileSystem</c>. Assign the <c>_flashPrefab</c>
    /// Inspector slot to any ParticleSystem prefab (Unity built-in Sparks preset is fine for
    /// prototyping). The pool creates <c>_poolSize</c> child instances at <c>Awake</c> and
    /// deactivates them immediately. <see cref="Spawn"/> re-activates one, plays it, then the
    /// <c>ParticleSystem</c> disables itself when finished via <c>Stop Action = Disable</c>.</para>
    ///
    /// <para><b>Round-robin:</b> the cursor advances unconditionally — no "is it still playing?"
    /// check. At the default 72fps VR fire rate (max 900 RPM = 15 shots/s) with pool size 6,
    /// each instance has 400ms between reuse, which exceeds any realistic muzzle flash duration
    /// (~100ms). Raise <c>_poolSize</c> if you extend flash durations past 300ms.</para>
    ///
    /// <para><b>WorldSpace position:</b> each spawn call re-parents to the pool root
    /// (<c>worldPositionStays: true</c>) and sets position/rotation directly. The instance is
    /// never parented to the weapon — the weapon moves and the flash should stay put at the
    /// shot origin.</para>
    /// </summary>
    /// <remarks>
    /// S1-007. GDD: core-fps-weapon-handling.md §Visual/Audio Requirements.
    /// Wiring checklist: see <c>Feature/WeaponHandling/README.md §8</c>.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MuzzleFlashPool : MonoBehaviour
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Muzzle Flash Pool")]
        [Tooltip(
            "ParticleSystem prefab to spawn. Assign the Unity built-in 'Sparks' preset " +
            "or a custom muzzle-flash particle asset. " +
            "The prefab's Stop Action MUST be set to Disable (pool relies on it for reset). " +
            "If null, Spawn() logs a warning and returns without spawning.")]
        [SerializeField] private ParticleSystem _flashPrefab;

        [Tooltip(
            "Number of ParticleSystem instances to pre-warm. " +
            "At 900 RPM (15 shots/s) each instance needs ≥67ms between reuse. " +
            "With typical 100ms flash duration and 6 instances: 400ms gap. " +
            "Raise if flashes are visually truncated on rapid fire.")]
        [Range(2, 16)]
        [SerializeField] private int _poolSize = 6;

        // ===================================================================
        // Private — pool state
        // ===================================================================

        /// <summary>Pre-warmed particle system instances. Built once in <c>Awake</c>.</summary>
        private ParticleSystem[] _pool;

        /// <summary>Round-robin acquisition cursor. Exposed <c>internal</c> for test assertions only.</summary>
        internal int ActiveCursor { get; private set; }

        // ===================================================================
        // Unity lifecycle
        // ===================================================================

        private void Awake()
        {
            BuildPool();
        }

        // ===================================================================
        // Public API
        // ===================================================================

        /// <summary>
        /// Spawns (re-activates) the next pool instance at <paramref name="position"/>
        /// with <paramref name="rotation"/> and plays it.
        ///
        /// <para>Safe to call every frame — internally advances a round-robin cursor.
        /// Does nothing if <c>_flashPrefab</c> is null or pool is not yet built.</para>
        /// </summary>
        /// <param name="position">World-space position of the muzzle tip.</param>
        /// <param name="rotation">World-space rotation of the muzzle (Z-forward = emitter direction).</param>
        public void Spawn(Vector3 position, Quaternion rotation)
        {
            if (_pool == null || _pool.Length == 0)
            {
                Debug.LogWarning(
                    "[MuzzleFlashPool] Pool not built — Spawn() called before Awake or " +
                    "after pool construction failed. Assign a ParticleSystem prefab to the " +
                    "_flashPrefab field on MuzzleFlashPool.",
                    this);
                return;
            }

            ParticleSystem flash = _pool[ActiveCursor];
            ActiveCursor = (ActiveCursor + 1) % _pool.Length;

            // Re-position in world space before activating.
            // SetParent with worldPositionStays keeps the world transform intact while
            // re-parenting to this GO's hierarchy so the instance is tidily organised.
            flash.transform.SetParent(transform, worldPositionStays: true);
            flash.transform.SetPositionAndRotation(position, rotation);

            // Activate and play.
            // ParticleSystem.Play() works on inactive GameObjects only after activation.
            flash.gameObject.SetActive(true);
            flash.Play(withChildren: true);
        }

        // ===================================================================
        // Pool construction
        // ===================================================================

        /// <summary>Creates and deactivates <c>_poolSize</c> child ParticleSystem instances.</summary>
        private void BuildPool()
        {
            if (_flashPrefab == null)
            {
                Debug.LogWarning(
                    "[MuzzleFlashPool] _flashPrefab is not assigned. " +
                    "Muzzle flash VFX will not play. " +
                    "Assign a ParticleSystem prefab in the Inspector.",
                    this);
                _pool = System.Array.Empty<ParticleSystem>();
                return;
            }

            _pool = new ParticleSystem[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                // Instantiate as a child of this GO so instances are tidy in the hierarchy.
                ParticleSystem instance = Instantiate(_flashPrefab, transform);
                instance.gameObject.name = $"MuzzleFlash_{i:D2}";

                // Enforce Stop Action = Disable so the instance returns to the inactive
                // state automatically when the particle burst completes.
                // This is the pool's reset mechanism — no coroutine required.
                var main = instance.main;
                main.stopAction = ParticleSystemStopAction.Disable;

                // Deactivate — ready for Spawn().
                instance.gameObject.SetActive(false);

                _pool[i] = instance;
            }
        }

        // ===================================================================
        // Test seam — internal injection for EditMode tests
        // ===================================================================

        /// <summary>
        /// Replaces the pool with pre-built instances and sets the cursor.
        /// Called only by EditMode unit tests; never call from production code.
        /// </summary>
        internal void InjectPrefabAndSize(ParticleSystem[] prebuiltPool, int cursorStart = 0)
        {
            _pool        = prebuiltPool;
            ActiveCursor = cursorStart;
        }
    }
}
