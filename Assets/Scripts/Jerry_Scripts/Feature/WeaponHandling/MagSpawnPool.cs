using System.Collections;
using UnityEngine;

namespace JerryScripts.Feature.WeaponHandling
{
    /// <summary>
    /// Round-robin pool of pre-warmed fresh-magazine actors that appear on the
    /// player's off-hand after a reload is initiated.
    ///
    /// <para><b>Placement:</b> add as a component on the same <c>_Systems</c>
    /// GameObject that hosts <c>MagDropPool</c>, <c>MuzzleFlashPool</c>,
    /// <c>AudioFeedbackService</c>, and <c>ProjectileSystem</c>. Assign
    /// <c>_magPrefab</c> to any GameObject prefab that carries an
    /// <c>XRGrabInteractable</c> (or a simple capsule for prototyping). The pool
    /// pre-warms <c>_poolSize</c> child instances at <c>Awake</c>, deactivates
    /// them immediately, and reuses them on every spawn — zero
    /// <c>Instantiate</c>/<c>Destroy</c> calls in the combat loop (GDD Rule 11).</para>
    ///
    /// <para><b>Lifecycle per spawn:</b>
    /// 1. Caller supplies an off-hand attach transform and a delay (seconds).
    /// 2. A coroutine waits <c>delay</c> seconds, then acquires the next instance
    ///    (round-robin), parents it to the attach transform, activates it, and
    ///    exposes it via <see cref="ActiveSpawnedMag"/>.
    /// 3. When <see cref="ReturnActive"/> is called (magazine was inserted or
    ///    dropped), the instance is deactivated and unparented.</para>
    ///
    /// <para><b>Round-robin:</b> cursor advances unconditionally. At one spawn per
    /// reload with pool size 2, each instance has at least one full reload cycle
    /// (typically 3–10 s) between reuse — truncation will not occur under normal
    /// gameplay. Raise <c>_poolSize</c> if players somehow trigger two reloads
    /// before the first magazine is inserted.</para>
    /// </summary>
    /// <remarks>
    /// S2-001. GDD: core-fps-weapon-handling.md §Reload Mechanic, Rules 10–11.
    /// Wiring checklist: add <c>MagSpawnPool</c> component to the <c>_Systems</c> GO;
    /// drag a magazine prefab into <c>_magPrefab</c>; leave Pool Size at default 2.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MagSpawnPool : MonoBehaviour
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Mag Spawn Pool")]
        [Tooltip(
            "Magazine GameObject prefab to spawn on the off-hand. Any rigid mesh " +
            "works — a capsule primitive is sufficient for prototyping. " +
            "If null, Spawn() logs a warning and skips the spawn.")]
        [SerializeField] private GameObject _magPrefab;

        [Tooltip(
            "Number of magazine instances to pre-warm. Raise if players can " +
            "initiate more than one reload before the first magazine is inserted.")]
        [Range(2, 4)]
        [SerializeField] private int _poolSize = 2;

        [Header("Generated Weapon Capacity")]
        [SerializeField] private int _fullGripCapacity = 15;

        // ===================================================================
        // Private — pool state
        // ===================================================================

        /// <summary>Pre-warmed magazine instances. Built once in <c>Awake</c>.</summary>
        private GameObject[] _pool;

        /// <summary>Round-robin acquisition cursor. Exposed <c>internal</c> for test assertions only.</summary>
        internal int ActiveCursor { get; private set; }

        /// <summary>
        /// The magazine instance currently active on the player's off-hand.
        /// Null when no magazine has been spawned or after <see cref="ReturnActive"/>
        /// is called. Read by <c>MagWellSocket</c> to perform proximity checks and
        /// by unit tests to assert spawn state.
        /// </summary>
        public GameObject ActiveSpawnedMag { get; private set; }

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
        /// Spawns (re-activates) the next pool instance on <paramref name="attachPoint"/>
        /// after <paramref name="delaySeconds"/> have elapsed.
        ///
        /// <para>The instance is parented to <paramref name="attachPoint"/>, positioned
        /// at local-zero, and exposed via <see cref="ActiveSpawnedMag"/> once the delay
        /// completes. Null-safe — does nothing (logs a warning) if <c>_magPrefab</c> is
        /// null or the pool is not yet built.</para>
        /// </summary>
        /// <param name="attachPoint">Off-hand transform to parent the magazine to.</param>
        /// <param name="delaySeconds">Seconds to wait before the magazine appears (GDD default: 0.2s).</param>
        public void Spawn(Transform attachPoint, float delaySeconds)
        {
            Spawn(attachPoint, delaySeconds, _fullGripCapacity);
        }

        public void Spawn(Transform attachPoint, float delaySeconds, int magazineCapacity)
        {
            if (_pool == null || _pool.Length == 0)
            {
                Debug.LogWarning(
                    "[MagSpawnPool] Pool not built — Spawn() called before Awake or " +
                    "after pool construction failed. Assign a GameObject prefab to the " +
                    "_magPrefab field on MagSpawnPool.",
                    this);
                return;
            }

            if (attachPoint == null)
            {
                Debug.LogWarning(
                    "[MagSpawnPool] Spawn() called with a null attachPoint. " +
                    "Supply the off-hand controller transform.",
                    this);
                return;
            }

            StartCoroutine(SpawnAfterDelay(attachPoint, delaySeconds, magazineCapacity));
        }

        /// <summary>
        /// Returns the currently active spawned magazine to the pool (deactivates it,
        /// unparents, clears <see cref="ActiveSpawnedMag"/>).
        ///
        /// <para>Call this when the magazine has been inserted into the weapon or
        /// dropped by the player. Safe to call when <see cref="ActiveSpawnedMag"/>
        /// is already null (no-op).</para>
        /// </summary>
        public void ReturnActive()
        {
            if (ActiveSpawnedMag == null) return;

            ActiveSpawnedMag.transform.SetParent(transform, worldPositionStays: false);
            ActiveSpawnedMag.SetActive(false);
            ActiveSpawnedMag = null;
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
                    "[MagSpawnPool] _magPrefab is not assigned. " +
                    "Off-hand magazine spawn will not play. " +
                    "Assign a GameObject prefab in the Inspector.",
                    this);
                _pool = System.Array.Empty<GameObject>();
                return;
            }

            _pool = new GameObject[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                GameObject instance = Instantiate(_magPrefab, transform);
                instance.name = $"MagSpawn_{i:D2}";
                instance.SetActive(false);
                _pool[i] = instance;
            }
        }

        // ===================================================================
        // Coroutine — delayed spawn
        // ===================================================================

        private IEnumerator SpawnAfterDelay(Transform attachPoint, float delaySeconds, int magazineCapacity)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            // Return any previously active magazine before acquiring the next slot.
            ReturnActive();

            GameObject instance = _pool[ActiveCursor];
            ActiveCursor = (ActiveCursor + 1) % _pool.Length;

            instance.SetActive(false);  // ensure clean state before reparenting
            instance.transform.SetParent(attachPoint, worldPositionStays: false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            ApplyMagazineCapacity(instance, magazineCapacity);
            instance.SetActive(true);

            ActiveSpawnedMag = instance;
        }

        private static void ApplyMagazineCapacity(GameObject instance, int magazineCapacity)
        {
            Component magazine = instance.GetComponent("WeaponMagazine");
            if (magazine == null)
            {
                Component[] childComponents = instance.GetComponentsInChildren<Component>(true);
                foreach (Component component in childComponents)
                {
                    if (component != null && component.GetType().Name == "WeaponMagazine")
                    {
                        magazine = component;
                        break;
                    }
                }
            }

            if (magazine == null)
            {
                return;
            }

            System.Reflection.MethodInfo setCapacityMethod = magazine.GetType().GetMethod("SetCapacity", new[] { typeof(int), typeof(bool) });
            setCapacityMethod?.Invoke(magazine, new object[] { magazineCapacity, true });
        }

        // ===================================================================
        // Test seam — internal injection for EditMode tests
        // ===================================================================

        /// <summary>
        /// Replaces the pool with pre-built instances and sets the cursor.
        /// Called only by EditMode unit tests; never call from production code.
        /// </summary>
        internal void InjectPool(GameObject[] prebuiltPool, int cursorStart = 0)
        {
            _pool        = prebuiltPool;
            ActiveCursor = cursorStart;
        }

        /// <summary>
        /// Directly sets <see cref="ActiveSpawnedMag"/> to a pre-built instance.
        /// Called only by EditMode unit tests to simulate a post-spawn state.
        /// </summary>
        internal void InjectActiveSpawnedMag(GameObject instance)
        {
            ActiveSpawnedMag = instance;
        }
    }
}
