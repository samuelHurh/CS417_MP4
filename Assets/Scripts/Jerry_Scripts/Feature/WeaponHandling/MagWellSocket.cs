using JerryScripts.Foundation;
using UnityEngine;

namespace JerryScripts.Feature.WeaponHandling
{
    /// <summary>
    /// Per-frame proximity detector that monitors the distance between the
    /// player's active spawned magazine (from <see cref="MagSpawnPool"/>) and
    /// the mag-well socket transform on the weapon, and calls
    /// <see cref="IMagInsertReceiver.CompleteReload"/> when they are within
    /// <see cref="WeaponData.MagInsertionRadius"/> metres.
    ///
    /// <para><b>Placement:</b> add this component to the weapon prefab root
    /// (same GameObject as <c>WeaponInstance</c>). Wire the Inspector fields:
    /// <list type="bullet">
    ///   <item><c>_magWellTransform</c> — the mag-well socket child transform</item>
    ///   <item><c>_weaponData</c> — the same <c>WeaponData</c> asset as the weapon</item>
    ///   <item><c>_receiver</c> — drag the <c>WeaponInstance</c> component here
    ///     (it implements <c>IMagInsertReceiver</c>)</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Activation pattern:</b> this component is <c>enabled</c> only while
    /// the weapon is in <c>WeaponInstanceState.Reloading</c>. <c>WeaponInstance</c>
    /// enables it on <c>BeginReload</c> and disables it when <c>CompleteReload</c>
    /// returns (or the weapon leaves the Reloading state). This avoids the cost of
    /// a proximity check on every frame during normal firing.</para>
    ///
    /// <para><b>Dependency injection:</b> <c>MagSpawnPool</c> is resolved once at
    /// <c>Awake</c> via <c>FindAnyObjectByType</c> — same null-safe pattern used by
    /// <c>WeaponInstance</c>. The pool reference is refreshed on each <c>OnEnable</c>
    /// if the initial resolve fails, so late-arriving <c>_Systems</c> GameObjects are
    /// handled gracefully.</para>
    /// </summary>
    /// <remarks>
    /// S2-001. GDD: core-fps-weapon-handling.md §Reload Mechanic, Rules 10–12.
    /// Wiring checklist: see Feature/WeaponHandling/README.md §MagWellSocket.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MagWellSocket : MonoBehaviour
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Mag Well Socket")]
        [Tooltip("Transform at the mag-well opening. Distance is measured from here.")]
        [SerializeField] private Transform _magWellTransform;

        [Tooltip("WeaponData asset — supplies MagInsertionRadius.")]
        [SerializeField] private WeaponData _weaponData;

        [Tooltip(
            "The component on this prefab that implements IMagInsertReceiver. " +
            "Drag the WeaponInstance component here.")]
        [SerializeField] private MonoBehaviour _receiverObject;

        // ===================================================================
        // Private — resolved references
        // ===================================================================

        private IMagInsertReceiver _receiver;
        private MagSpawnPool       _magSpawnPool;

        // ===================================================================
        // Unity lifecycle
        // ===================================================================

        private void Awake()
        {
            ResolveReceiver();
            _magSpawnPool = FindAnyObjectByType<MagSpawnPool>();
            ValidateReferences();

            // Start disabled — WeaponInstance enables this when Reloading begins.
            enabled = false;
        }

        private void OnEnable()
        {
            // Re-resolve pool in case it arrived after this component's Awake.
            if (_magSpawnPool == null)
                _magSpawnPool = FindAnyObjectByType<MagSpawnPool>();
        }

        private void Update()
        {
            if (_magSpawnPool == null) return;

            GameObject mag = _magSpawnPool.ActiveSpawnedMag;
            if (mag == null) return;

            Transform magWell = _magWellTransform != null ? _magWellTransform : transform;
            float     radius  = _weaponData != null ? _weaponData.MagInsertionRadius : 0.05f;

            if (Vector3.Distance(mag.transform.position, magWell.position) <= radius)
            {
                // Return the magazine to the pool before notifying the receiver.
                // This prevents a second proximity match on the same frame.
                _magSpawnPool.ReturnActive();

                // Disable self immediately — receiver's CompleteReload will
                // change FSM state, but disabling here is defensive.
                enabled = false;

                _receiver?.CompleteReload();
            }
        }

        // ===================================================================
        // Reference resolution
        // ===================================================================

        private void ResolveReceiver()
        {
            if (_receiverObject == null) return;

            _receiver = _receiverObject as IMagInsertReceiver;

            if (_receiver == null)
            {
                Debug.LogError(
                    $"[MagWellSocket] '{_receiverObject.name}' does not implement " +
                    "IMagInsertReceiver. Drag a WeaponInstance component into the " +
                    "_receiverObject field.",
                    this);
            }
        }

        private void ValidateReferences()
        {
            if (_magWellTransform == null)
                Debug.LogWarning(
                    "[MagWellSocket] _magWellTransform is not assigned — " +
                    "proximity check will use this GO's transform as fallback.",
                    this);

            if (_weaponData == null)
                Debug.LogWarning(
                    "[MagWellSocket] _weaponData is not assigned — " +
                    "MagInsertionRadius will default to 0.05m.",
                    this);

            if (_receiverObject == null)
                Debug.LogError(
                    "[MagWellSocket] _receiverObject is not assigned — " +
                    "CompleteReload will never be called.",
                    this);

            if (_magSpawnPool == null)
                Debug.LogWarning(
                    "[MagWellSocket] MagSpawnPool not found in scene — " +
                    "ensure a MagSpawnPool component is present on the _Systems GO.",
                    this);
        }

        // ===================================================================
        // Test seam — internal injection for EditMode tests
        // ===================================================================

        /// <summary>
        /// Injects the resolved references directly, bypassing Awake scene searches.
        /// Called only by EditMode unit tests; never call from production code.
        /// </summary>
        internal void InjectDependencies(
            MagSpawnPool       magSpawnPool,
            IMagInsertReceiver receiver,
            Transform          magWellTransform,
            WeaponData         weaponData)
        {
            _magSpawnPool     = magSpawnPool;
            _receiver         = receiver;
            _magWellTransform = magWellTransform;
            _weaponData       = weaponData;
        }
    }
}
