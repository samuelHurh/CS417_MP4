using UnityEngine;

namespace JerryScripts.Feature.WeaponHandling
{
    /// <summary>
    /// Round-robin pool of pre-warmed magazine drop actors.
    ///
    /// <para><b>Placement:</b> add as a component on the same <c>_Systems</c> GameObject that
    /// hosts <c>AudioFeedbackService</c>, <c>ProjectileSystem</c>, and <c>MuzzleFlashPool</c>.
    /// Assign the <c>_magPrefab</c> Inspector slot to any magazine GameObject prefab (a simple
    /// capsule primitive is sufficient for prototyping). The pool pre-warms <c>_poolSize</c>
    /// child instances at <c>Awake</c>, disables them immediately, and reuses them on every
    /// eject — zero <c>Instantiate</c>/<c>Destroy</c> calls in the combat loop (GDD Rule 11).</para>
    ///
    /// <para><b>Lifecycle per eject:</b>
    /// 1. Acquire the next instance (round-robin), enable it, position it at the mag-well.
    /// 2. Un-kinematic so it falls naturally.
    /// 3. Start a self-managed coroutine that disables the instance after
    ///    <see cref="WeaponData.MagazinePersistSeconds"/> — no <c>Destroy</c> required.</para>
    ///
    /// <para><b>Round-robin:</b> cursor advances unconditionally. At two shots per reload cycle
    /// (max ~1 mag drop per second) with pool size 4, each instance has at least 4 s between
    /// reuse — well beyond the 8 s default persist time, so truncation does not occur under
    /// normal gameplay. Raise <c>_poolSize</c> if players chain reloads faster than persist time.</para>
    /// </summary>
    /// <remarks>
    /// S1-009. GDD: core-fps-weapon-handling.md §Reload Mechanic / Rule 11.
    /// Wiring checklist: add <c>MagDropPool</c> component to the <c>_Systems</c> GO;
    /// drag the magazine prefab into <c>_magPrefab</c>; leave Pool Size at default 4.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MagDropPool : MonoBehaviour
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Mag Drop Pool")]
        [Tooltip(
            "Magazine GameObject prefab to eject. Any rigid mesh with a Rigidbody component " +
            "works — a capsule primitive is sufficient for prototyping. " +
            "The Rigidbody MUST be present on the root; the pool sets isKinematic on it. " +
            "If null, Eject() logs a warning and returns without ejecting.")]
        [SerializeField] private GameObject _magPrefab;

        [Tooltip(
            "Number of magazine instances to pre-warm. Raise if players can eject more than " +
            "one magazine within MagazinePersistSeconds of each other (unlikely in normal gameplay).")]
        [Range(2, 8)]
        [SerializeField] private int _poolSize = 4;

        // ===================================================================
        // Private — pool state
        // ===================================================================

        /// <summary>Pre-warmed magazine instances. Built once in <c>Awake</c>.</summary>
        private GameObject[] _pool;

        /// <summary>Cached Rigidbody per pool slot — avoids <c>GetComponent</c> on each eject.</summary>
        private Rigidbody[] _rigidbodies;

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
        /// Ejects (re-activates) the next pool instance at <paramref name="position"/> with
        /// <paramref name="rotation"/>, enables physics, and schedules auto-disable after
        /// <paramref name="persistSeconds"/>.
        ///
        /// <para>Safe to call every reload — internally advances a round-robin cursor.
        /// Does nothing (logs a warning) if <c>_magPrefab</c> is null or pool is not yet built.</para>
        /// </summary>
        /// <param name="position">World-space position of the mag-well socket.</param>
        /// <param name="rotation">World-space rotation of the mag-well socket.</param>
        /// <param name="persistSeconds">Seconds before the instance is returned to the pool (disabled).</param>
        public void Eject(Vector3 position, Quaternion rotation, float persistSeconds)
        {
            if (_pool == null || _pool.Length == 0)
            {
                Debug.LogWarning(
                    "[MagDropPool] Pool not built — Eject() called before Awake or after " +
                    "pool construction failed. Assign a GameObject prefab to the _magPrefab " +
                    "field on MagDropPool.",
                    this);
                return;
            }

            // Recycle the slot: disable it first so any in-progress physics fully stops
            // before repositioning. Prevents the old position appearing for one frame.
            GameObject instance = _pool[ActiveCursor];
            Rigidbody  rb       = _rigidbodies[ActiveCursor];

            ActiveCursor = (ActiveCursor + 1) % _pool.Length;

            // Reset Rigidbody before repositioning — velocity from the previous eject
            // must not carry over when the instance is recycled before persist expires.
            // Order matters: zero velocities BEFORE setting kinematic. Setting velocities
            // on an already-kinematic body produces a Unity warning and is undefined.
            if (rb != null)
            {
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic     = true;
            }

            instance.SetActive(false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            // Un-kinematic so it falls naturally (GDD Rule 11: mag drops with physics).
            if (rb != null)
                rb.isKinematic = false;

            // Schedule return-to-pool via coroutine (no Destroy — GDD no-Instantiate rule).
            StartCoroutine(DisableAfterDelay(instance, rb, persistSeconds));
        }

        // ===================================================================
        // Pool construction
        // ===================================================================

        /// <summary>Creates and deactivates <c>_poolSize</c> child magazine instances.</summary>
        private void BuildPool()
        {
            if (_magPrefab == null)
            {
                Debug.LogWarning(
                    "[MagDropPool] _magPrefab is not assigned. " +
                    "Magazine drop VFX will not play. " +
                    "Assign a GameObject prefab in the Inspector.",
                    this);
                _pool       = System.Array.Empty<GameObject>();
                _rigidbodies = System.Array.Empty<Rigidbody>();
                return;
            }

            _pool        = new GameObject[_poolSize];
            _rigidbodies = new Rigidbody[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                GameObject instance = Instantiate(_magPrefab, transform);
                instance.name = $"MagDrop_{i:D2}";

                // Cache the Rigidbody component now — avoids GetComponent on each eject.
                Rigidbody rb = instance.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    Debug.LogWarning(
                        $"[MagDropPool] Prefab '{_magPrefab.name}' has no Rigidbody. " +
                        "Magazine drops will not have physics. Add a Rigidbody to the prefab.",
                        this);
                }
                else
                {
                    rb.isKinematic = true;  // start kinematic; Eject() enables physics
                }

                // Deactivate — ready for Eject().
                instance.SetActive(false);

                _pool[i]        = instance;
                _rigidbodies[i] = rb;
            }
        }

        // ===================================================================
        // Coroutine — auto-disable after persist time
        // ===================================================================

        /// <summary>
        /// Waits <paramref name="delay"/> seconds then returns <paramref name="instance"/>
        /// to the pool (disabled + kinematic). Replaces <c>Destroy(ejectedMag, persistSeconds)</c>.
        /// </summary>
        private System.Collections.IEnumerator DisableAfterDelay(
            GameObject instance,
            Rigidbody  rb,
            float      delay)
        {
            yield return new WaitForSeconds(delay);

            if (instance != null)
            {
                if (rb != null)
                    rb.isKinematic = true;

                instance.SetActive(false);
            }
        }

        // ===================================================================
        // Test seam — internal injection for EditMode tests
        // ===================================================================

        /// <summary>
        /// Replaces the pool with pre-built instances and sets the cursor.
        /// Called only by EditMode unit tests; never call from production code.
        /// </summary>
        internal void InjectPool(GameObject[] prebuiltPool, Rigidbody[] rigidbodies, int cursorStart = 0)
        {
            _pool        = prebuiltPool;
            _rigidbodies = rigidbodies;
            ActiveCursor = cursorStart;
        }
    }
}
