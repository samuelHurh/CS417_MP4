using JerryScripts.Core.Projectile;
using JerryScripts.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace JerryScripts.Feature.WeaponHandling
{
    /// <summary>
    /// Runtime behaviour for a single weapon in the world.
    /// Owns the weapon FSM (<see cref="WeaponInstanceState"/>), ammo tracking,
    /// recoil, and all XRI grab/drop callbacks.
    ///
    /// <para><b>Dependencies (injected via inspector — no Find() calls):</b></para>
    /// <list type="bullet">
    ///   <item><see cref="WeaponData"/> — stat source of truth</item>
    ///   <item><see cref="IRigStateProvider"/> — gates interaction to Active rig state</item>
    ///   <item><see cref="IRigControllerProvider"/> — supplies haptic players</item>
    ///   <item><see cref="IMountPointProvider"/> — supplies holster/hand transforms</item>
    ///   <item><see cref="XRGrabInteractable"/> — XRI grab surface on this prefab</item>
    /// </list>
    ///
    /// <para>Wire all serialised fields in the Inspector. See <c>README.md</c> in this folder
    /// for step-by-step pistol prefab setup.</para>
    /// </summary>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class WeaponInstance : MonoBehaviour
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Data")]
        [Tooltip("ScriptableObject that defines all tuning values for this weapon.")]
        [SerializeField] private WeaponData _data;

        // Rig reference is auto-resolved in Awake via FindFirstObjectByType<PlayerRig>.
        // No inspector slot — removed due to Unity's component picker UX quirks with
        // the concrete type binding. The scene search runs once at startup and is
        // cheap for a single-player VR scene. If multiple PlayerRigs ever exist in
        // one scene, refactor to a static singleton on PlayerRig.
        private PlayerRig _playerRig;

        [Header("Weapon Transforms")]
        [Tooltip("Transform at the muzzle tip. FireHitscan ray originates here.")]
        [SerializeField] private Transform _muzzleTransform;

        [Tooltip("Mag-well socket transform. Magazine proximity is tested against this.")]
        [SerializeField] private Transform _magWellTransform;

        [Header("Input — Holding Hand")]
        [Tooltip("InputActionReference bound to the primary button (magazine release).")]
        [SerializeField] private InputActionReference _primaryButtonAction;

        [Tooltip("InputActionReference bound to the secondary button (slide rack).")]
        [SerializeField] private InputActionReference _secondaryButtonAction;

        [Tooltip("InputActionReference bound to the trigger (fire).")]
        [SerializeField] private InputActionReference _triggerAction;

        // ===================================================================
        // Runtime state — public read-only
        // ===================================================================

        /// <summary>Current FSM state of this weapon.</summary>
        public WeaponInstanceState CurrentState { get; private set; } = WeaponInstanceState.Holstered;

        /// <summary>Rounds currently loaded in the magazine (0 to <see cref="WeaponData.MagCapacity"/>).</summary>
        public int CurrentAmmo { get; private set; }

        // ===================================================================
        // Private — cached references
        // ===================================================================

        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
        private Rigidbody _rigidbody;

        private IRigStateProvider _rigStateProvider;
        private IRigControllerProvider _rigControllerProvider;
        private IMountPointProvider _mountPointProvider;

        /// <summary>
        /// Projectile System service resolved at Awake. Null-safe — hitscan calls are skipped
        /// (with a debug log) when the service is absent. S1-005.
        /// </summary>
        private IProjectileService _projectileService;

        // ===================================================================
        // Private — runtime tracking
        // ===================================================================

        /// <summary>True if the grab was by the right hand (used to select correct haptic player).</summary>
        private bool _heldInRightHand;

        /// <summary>World-space time of the last shot fired.</summary>
        private float _lastShotTime = -999f;

        /// <summary>Whether <see cref="CurrentAmmo"/> was 0 when the reload began (slide-back required).</summary>
        private bool _wasEmptyBeforeReload;

        // ===================================================================
        // Recoil state
        // ===================================================================

        private Quaternion _recoilOffset = Quaternion.identity;
        private Quaternion _recoilTarget = Quaternion.identity;
        private float _recoilDecayElapsed;

        // ===================================================================
        // Unity lifecycle
        // ===================================================================

        private void Awake()
        {
            _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            _rigidbody        = GetComponent<Rigidbody>();

            // GDD Rule 3: only one hand may hold the pistol at a time.
            _grabInteractable.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Single;

            // Auto-resolve rig reference from the scene (no inspector wiring needed).
            // FindAnyObjectByType is the Unity 6+ replacement for FindFirstObjectByType;
            // order is not guaranteed but we only expect one PlayerRig per scene.
            _playerRig = FindAnyObjectByType<PlayerRig>();
            ResolveInterfaces();

            // Auto-resolve Projectile System (S1-005). Null-safe — hitscan is skipped
            // until the system is present in the scene (consistent with PlayerRig pattern).
            _projectileService = FindAnyObjectByType<ProjectileSystem>();

            ValidateReferences();

            // Start loaded — GDD §Ammo State
            if (_data != null)
                CurrentAmmo = _data.MagCapacity;

            // Begin holstered at scene start
            EnterState(WeaponInstanceState.Holstered);
        }

        private void OnEnable()
        {
            _grabInteractable.selectEntered.AddListener(OnGrabbed);
            _grabInteractable.selectExited.AddListener(OnReleased);

            if (_rigStateProvider != null)
                _rigStateProvider.OnStateChanged += OnRigStateChanged;
        }

        private void OnDisable()
        {
            _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            _grabInteractable.selectExited.RemoveListener(OnReleased);

            if (_rigStateProvider != null)
                _rigStateProvider.OnStateChanged -= OnRigStateChanged;

            // Always unsubscribe input when disabled — prevents ghost callbacks
            UnsubscribeInputActions();
        }

        private void Update()
        {
            UpdateRecoilDecay();
        }

        // ===================================================================
        // XRI callbacks
        // ===================================================================

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            // Rig-state gate: refuse grab if rig is not Active (GDD Rule 2)
            if (_rigStateProvider != null &&
                _rigStateProvider.CurrentState != RigState.Active)
            {
                // Force XRI to drop the selection immediately
                _grabInteractable.interactionManager.SelectExit(
                    args.interactorObject, _grabInteractable);
                return;
            }

            // Determine hand by comparing the interactor's transform to rig controller transforms.
            // No string matching — compare object references.
            _heldInRightHand = DetermineIfRightHand(args.interactorObject.transform);

            SubscribeInputActions();
            EnterState(WeaponInstanceState.Held);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (CurrentState == WeaponInstanceState.Held ||
                CurrentState == WeaponInstanceState.Firing ||
                CurrentState == WeaponInstanceState.Reloading ||
                CurrentState == WeaponInstanceState.SlideBack)
            {
                UnsubscribeInputActions();
                EvaluateDropDestination();
            }
        }

        // ===================================================================
        // Input action subscriptions
        // ===================================================================

        private void SubscribeInputActions()
        {
            if (_triggerAction != null)
            {
                _triggerAction.action.Enable();
                _triggerAction.action.performed += OnTriggerPerformed;
            }

            if (_primaryButtonAction != null)
            {
                _primaryButtonAction.action.Enable();
                _primaryButtonAction.action.performed += OnPrimaryButtonPerformed;
            }

            if (_secondaryButtonAction != null)
            {
                _secondaryButtonAction.action.Enable();
                _secondaryButtonAction.action.performed += OnSecondaryButtonPerformed;
            }
        }

        private void UnsubscribeInputActions()
        {
            if (_triggerAction != null)
                _triggerAction.action.performed -= OnTriggerPerformed;

            if (_primaryButtonAction != null)
                _primaryButtonAction.action.performed -= OnPrimaryButtonPerformed;

            if (_secondaryButtonAction != null)
                _secondaryButtonAction.action.performed -= OnSecondaryButtonPerformed;
        }

        // ===================================================================
        // Input handlers
        // ===================================================================

        private void OnTriggerPerformed(InputAction.CallbackContext _)
        {
            if (CurrentState != WeaponInstanceState.Held) return;
            if (!IsRigActive()) return;

            if (CurrentAmmo <= 0)
            {
                // Dry-fire (GDD Rule 8)
                TriggerHaptic(_data.HapticDryFireAmplitude, _data.HapticFireDuration);
                // TODO: Audio system call — dry-fire click SFX
                return;
            }

            float timeSinceLastShot = Time.time - _lastShotTime;
            if (timeSinceLastShot < _data.FireInterval) return;

            ExecuteFire();
        }

        private void OnPrimaryButtonPerformed(InputAction.CallbackContext _)
        {
            // Only valid from Held (GDD Rule 10 — Drop mag)
            if (CurrentState != WeaponInstanceState.Held) return;
            if (!IsRigActive()) return;

            _wasEmptyBeforeReload = (CurrentAmmo == 0);
            BeginReload();
        }

        private void OnSecondaryButtonPerformed(InputAction.CallbackContext _)
        {
            // Rack slide (GDD Rule 12) — only from SlideBack
            if (CurrentState != WeaponInstanceState.SlideBack) return;
            if (!IsRigActive()) return;

            // TODO: Audio system call — slide rack SFX + haptic
            EnterState(WeaponInstanceState.Held);
        }

        // ===================================================================
        // Fire
        // ===================================================================

        private void ExecuteFire()
        {
            EnterState(WeaponInstanceState.Firing);

            CurrentAmmo = Mathf.Max(0, CurrentAmmo - 1);
            _lastShotTime = Time.time;

            // Hitscan direction captured HERE, before recoil is applied (GDD Rule 15).
            // _muzzleTransform.position/forward are read before ApplyRecoilKick() mutates
            // the weapon's local rotation — this is the canonical fire vector.
            if (_projectileService != null && _muzzleTransform != null && _data != null)
            {
                _projectileService.FireHitscan(_muzzleTransform, _data);
            }
            else
            {
                Debug.LogWarning(
                    "[WeaponInstance] FireHitscan skipped — IProjectileService not resolved. " +
                    "Ensure a ProjectileSystem component is present in the scene. S1-005.",
                    this);
            }

            // TODO (S1-006): IAudioFeedbackService.PostFeedbackEvent(WeaponFire, _muzzleTransform.position)
            // TODO (VFX): Muzzle flash VFX spawn at _muzzleTransform

            // Haptic (GDD Rule 7)
            TriggerHaptic(_data.HapticFireAmplitude, _data.HapticFireDuration);

            // Recoil (GDD Rules 15–16) — applied after hitscan sample
            ApplyRecoilKick();

            // Firing is a transient sub-state; return to Held immediately (GDD Rule 6)
            EnterState(WeaponInstanceState.Held);
        }

        // ===================================================================
        // Reload
        // ===================================================================

        private void BeginReload()
        {
            EnterState(WeaponInstanceState.Reloading);

            // Drop current magazine with physics
            if (_data.MagazinePrefab != null)
            {
                // Instantiate the ejected magazine at the mag-well position
                var ejectedMag = Instantiate(
                    _data.MagazinePrefab,
                    _magWellTransform.position,
                    _magWellTransform.rotation);

                // Enable physics so it falls
                var rb = ejectedMag.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = false;

                // Auto-destroy after persist time (GDD Rule 11)
                Destroy(ejectedMag, _data.MagazinePersistSeconds);
            }

            // TODO: Audio system call — mag drop SFX
            // TODO: Spawn fresh magazine on offhand after _data.MagSpawnDelay seconds (Coroutine, owned by this class)

            // NOTE: Transition to Held or SlideBack happens when mag-well proximity is detected.
            // That logic is triggered by the magazine prefab reporting back (or a separate
            // MagWellSocket component calling CompleteReload on this WeaponInstance).
        }

        /// <summary>
        /// Called by the mag-well socket component when a magazine is inserted.
        /// Resets ammo and advances the FSM per GDD Rules 10 and 12.
        /// </summary>
        public void CompleteReload()
        {
            if (CurrentState != WeaponInstanceState.Reloading)
            {
                Debug.LogWarning(
                    "[WeaponInstance] CompleteReload called outside Reloading state — ignored.",
                    this);
                return;
            }

            CurrentAmmo = _data.MagCapacity;

            // TODO: Audio system call — mag insertion click SFX + haptic pulse

            // GDD Rule 12: tactical reload (was not empty) skips slide-back
            if (_wasEmptyBeforeReload)
                EnterState(WeaponInstanceState.SlideBack);
            else
                EnterState(WeaponInstanceState.Held);
        }

        // ===================================================================
        // Drop / holster routing
        // ===================================================================

        private void EvaluateDropDestination()
        {
            if (_mountPointProvider == null)
            {
                EnterState(WeaponInstanceState.Dropped);
                return;
            }

            float snapRadius = _data != null ? _data.HolsterSnapRadius : 0.15f;

            // Check hip holsters then chest (GDD Rule 17 — HipR and ChestL)
            if (IsWithinRadius(_mountPointProvider.RightHipHolster, snapRadius))
            {
                SnapToMount(_mountPointProvider.RightHipHolster);
                return;
            }

            if (IsWithinRadius(_mountPointProvider.LeftHipHolster, snapRadius))
            {
                SnapToMount(_mountPointProvider.LeftHipHolster);
                return;
            }

            if (IsWithinRadius(_mountPointProvider.ChestMountPoint, snapRadius))
            {
                SnapToMount(_mountPointProvider.ChestMountPoint);
                return;
            }

            // Not near any mount — free fall (GDD Rule 17)
            EnterState(WeaponInstanceState.Dropped);
        }

        private void SnapToMount(Transform mountPoint)
        {
            transform.SetParent(mountPoint, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            // TODO: Audio system call — holster click SFX
            EnterState(WeaponInstanceState.Holstered);
        }

        private bool IsWithinRadius(Transform target, float radius)
        {
            if (target == null) return false;
            return Vector3.Distance(transform.position, target.position) <= radius;
        }

        // ===================================================================
        // Recoil
        // ===================================================================

        private void ApplyRecoilKick()
        {
            if (_data == null) return;

            float pitch = _data.RecoilPitchBase;
            float yaw   = Random.Range(-_data.RecoilYawSpread, _data.RecoilYawSpread);

            // Accumulate (stack on top of any ongoing recoil decay)
            _recoilTarget = Quaternion.Euler(-pitch, yaw, 0f) * _recoilTarget;
            _recoilOffset = _recoilTarget;
            _recoilDecayElapsed = 0f;
        }

        private void UpdateRecoilDecay()
        {
            if (_recoilOffset == Quaternion.identity) return;
            if (_data == null || _data.RecoilRecoveryTime <= 0f)
            {
                _recoilOffset = Quaternion.identity;
                _recoilTarget = Quaternion.identity;
                return;
            }

            _recoilDecayElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_recoilDecayElapsed / _data.RecoilRecoveryTime);

            _recoilOffset = Quaternion.Slerp(_recoilTarget, Quaternion.identity, t);

            if (t >= 1f)
            {
                _recoilOffset = Quaternion.identity;
                _recoilTarget = Quaternion.identity;
            }

            // Apply as local rotation offset relative to the base tracking rotation.
            // XRGrabInteractable drives the base pose; we stack on top.
            transform.localRotation = _recoilOffset * transform.localRotation;
        }

        // ===================================================================
        // Fall-through safety (GDD Edge Case — weapon falls through floor)
        // ===================================================================

        private void LateUpdate()
        {
            if (CurrentState == WeaponInstanceState.Dropped &&
                transform.position.y < -2f)
            {
                // Teleport to default holster (GDD Edge Case §6)
                if (_mountPointProvider?.RightHipHolster != null)
                    SnapToMount(_mountPointProvider.RightHipHolster);
                else
                    transform.position = Vector3.zero; // fallback if no rig present
            }
        }

        // ===================================================================
        // FSM — state entry
        // ===================================================================

        private void EnterState(WeaponInstanceState newState)
        {
            if (CurrentState == newState) return;

            CurrentState = newState;

            switch (newState)
            {
                case WeaponInstanceState.Holstered:
                    _rigidbody.isKinematic = true;
                    _grabInteractable.enabled = true;
                    break;

                case WeaponInstanceState.Held:
                    _rigidbody.isKinematic = true;  // XRI owns the transform
                    _grabInteractable.enabled = true;
                    // Unparent from any holster mount
                    transform.SetParent(null);
                    break;

                case WeaponInstanceState.Firing:
                    // Transient — no Rigidbody/interactable changes; returns to Held immediately
                    break;

                case WeaponInstanceState.Reloading:
                    _rigidbody.isKinematic = true;  // hand still holds the frame
                    break;

                case WeaponInstanceState.SlideBack:
                    _rigidbody.isKinematic = true;
                    break;

                case WeaponInstanceState.Dropped:
                    transform.SetParent(null);
                    _rigidbody.isKinematic = false;
                    _grabInteractable.enabled = true;
                    break;
            }
        }

        // ===================================================================
        // Rig state gate
        // ===================================================================

        private void OnRigStateChanged(RigState newRigState)
        {
            // If rig dies or pauses while weapon is held, lock interaction
            if (newRigState != RigState.Active &&
                CurrentState == WeaponInstanceState.Held)
            {
                // Disable input receipt without changing weapon FSM state —
                // the weapon remains "Held" but trigger/buttons do nothing
                // (IsRigActive() check guards every action).
            }
        }

        private bool IsRigActive()
        {
            return _rigStateProvider == null ||
                   _rigStateProvider.CurrentState == RigState.Active;
        }

        // ===================================================================
        // Haptics
        // ===================================================================

        private void TriggerHaptic(float amplitude, float duration)
        {
            if (_rigControllerProvider == null || _data == null) return;

            HapticImpulsePlayer hapticPlayer = _heldInRightHand
                ? _rigControllerProvider.RightHaptics
                : _rigControllerProvider.LeftHaptics;

            hapticPlayer?.SendHapticImpulse(amplitude, duration);
        }

        // ===================================================================
        // Hand detection
        // ===================================================================

        /// <summary>
        /// Determines whether the grabbing interactor is the right-hand controller
        /// by comparing transform references to the rig's known controller transforms.
        /// Falls back to <c>true</c> (right hand) if the rig reference is absent.
        /// </summary>
        private bool DetermineIfRightHand(Transform interactorTransform)
        {
            if (_rigControllerProvider == null) return true;

            // Walk up the interactor hierarchy — some XRI rigs nest the interactor
            // one or two levels below the controller root
            Transform current = interactorTransform;
            while (current != null)
            {
                if (current == _rigControllerProvider.RightControllerTransform)
                    return true;
                if (current == _rigControllerProvider.LeftControllerTransform)
                    return false;
                current = current.parent;
            }

            // Could not match — assume right hand
            return true;
        }

        // ===================================================================
        // Interface resolution
        // ===================================================================

        private void ResolveInterfaces()
        {
            if (_playerRig == null) return;

            _rigStateProvider      = _playerRig;
            _rigControllerProvider = _playerRig;
            _mountPointProvider    = _playerRig;
        }

        // ===================================================================
        // Validation
        // ===================================================================

        private void ValidateReferences()
        {
            if (_data == null)
                Debug.LogError("[WeaponInstance] WeaponData is not assigned.", this);

            if (_playerRig == null)
                Debug.LogWarning(
                    "[WeaponInstance] PlayerRig reference not assigned — rig-state gating, " +
                    "haptics, and holster snapping will all be skipped. Drag the PlayerRig " +
                    "component from the XR Origin into the PlayerRig field on this component.",
                    this);

            if (_muzzleTransform == null)
                Debug.LogError("[WeaponInstance] MuzzleTransform is not assigned.", this);

            if (_magWellTransform == null)
                Debug.LogWarning(
                    "[WeaponInstance] MagWellTransform is not assigned — mag-well proximity cannot be checked.",
                    this);

            if (_triggerAction == null)
                Debug.LogError("[WeaponInstance] TriggerAction is not assigned — firing will not work.", this);

            if (_primaryButtonAction == null)
                Debug.LogWarning("[WeaponInstance] PrimaryButtonAction is not assigned — reload initiation will not work.", this);

            if (_secondaryButtonAction == null)
                Debug.LogWarning("[WeaponInstance] SecondaryButtonAction is not assigned — slide rack will not work.", this);
        }
    }
}
