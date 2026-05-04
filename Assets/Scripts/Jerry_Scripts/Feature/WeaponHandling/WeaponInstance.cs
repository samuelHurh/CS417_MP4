using JerryScripts.Core.Projectile;
using JerryScripts.Foundation;
using JerryScripts.Foundation.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
namespace JerryScripts.Feature.WeaponHandling
{
    /// <summary>
    /// Runtime behaviour for a single weapon in the world.
    /// Owns the weapon FSM (<see cref="WeaponInstanceState"/>), ammo tracking,
    /// recoil, and all XRI grab/drop callbacks.
    ///
    /// <para>Implements <see cref="IMagInsertReceiver"/> — the
    /// <see cref="MagWellSocket"/> component on this prefab calls
    /// <see cref="CompleteReload"/> when the player's off-hand magazine enters
    /// the mag-well proximity radius.</para>
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
    public sealed class WeaponInstance : MonoBehaviour, IMagInsertReceiver
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

        [Header("Visuals")]
        [Tooltip("Renderer for the magazine mesh on the pistol model. Disabled on eject, re-enabled on reload complete.")]
        [SerializeField] private Renderer _magMeshRenderer;

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

        /// <summary>Magazine capacity from generated grip size when present, otherwise <see cref="WeaponData.MagCapacity"/>.</summary>
        public int MagCapacity => GetEffectiveMagCapacity();

        /// <summary>
        /// Fired whenever <see cref="CurrentAmmo"/> changes (fire, reload, init).
        /// Args: (currentAmmo, magCapacity). HUD subscribes to display ammo count.
        /// </summary>
        public event System.Action<int, int> OnAmmoChanged;

        /// <summary>
        /// Fired when the weapon is grabbed or released by the player.
        /// Arg: <c>true</c> = equipped (grabbed), <c>false</c> = unequipped (released/dropped/holstered).
        /// HUD subscribes to show/hide ammo display.
        /// </summary>
        public event System.Action<bool> OnEquipChanged;

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

        /// <summary>
        /// Audio Feedback Service resolved at Awake. Null-safe — audio calls are skipped
        /// (with a debug log) when the service is absent. S1-006.
        /// </summary>
        private IAudioFeedbackService _audioService;

        /// <summary>
        /// Muzzle Flash Pool resolved at Awake. Null-safe — VFX spawn is silently skipped
        /// when the pool is absent (headless tests, scenes without the _Systems GO). S1-007.
        /// </summary>
        private MuzzleFlashPool _muzzleFlashPool;

        /// <summary>
        /// Mag Drop Pool resolved at Awake. Null-safe — magazine eject falls back to a
        /// single-frame disappear (no physics drop) when the pool is absent. S1-009.
        /// </summary>
        private MagDropPool _magDropPool;

        /// <summary>
        /// Mag Spawn Pool resolved at Awake. Null-safe — off-hand magazine spawn is
        /// skipped when the pool is absent. S2-001.
        /// </summary>
        private MagSpawnPool _magSpawnPool;

        /// <summary>
        /// Mag Well Socket resolved at Awake via GetComponent on this same GO.
        /// Enabled/disabled to gate the per-frame proximity check only while Reloading.
        /// </summary>
        private MagWellSocket _magWellSocket;

        // ===================================================================
        // Private — runtime tracking
        // ===================================================================

        /// <summary>True if the grab was by the right hand (used to select correct haptic player).</summary>
        private bool _heldInRightHand;

        /// <summary>World-space time of the last shot fired.</summary>
        private float _lastShotTime = -999f;

        /// <summary>Whether <see cref="CurrentAmmo"/> was 0 when the reload began (slide-back required).</summary>
        private bool _wasEmptyBeforeReload;

        /// <summary>True when rig is not Active — blocks grab/release/input processing.</summary>
        private bool _pauseFrozen;

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

            // Auto-resolve Audio Feedback Service (S1-006). Null-safe — audio calls are
            // skipped until the service is present in the scene.
            _audioService = FindAnyObjectByType<AudioFeedbackService>();

            // Auto-resolve Muzzle Flash Pool (S1-007). Null-safe — VFX spawn is skipped
            // until the pool is present in the scene (same pattern as AudioFeedbackService).
            _muzzleFlashPool = FindAnyObjectByType<MuzzleFlashPool>();

            // Auto-resolve Mag Drop Pool (S1-009). Null-safe — magazine eject is skipped
            // (no physics drop) until the pool is present in the scene.
            _magDropPool = FindAnyObjectByType<MagDropPool>();

            // Auto-resolve Mag Spawn Pool (S2-001). Null-safe — off-hand magazine spawn
            // is skipped until the pool is present in the scene.
            _magSpawnPool = FindAnyObjectByType<MagSpawnPool>();

            // Resolve MagWellSocket on this same GO — optional, no error if absent.
            _magWellSocket = GetComponent<MagWellSocket>();

            ValidateReferences();

            // Start loaded — GDD §Ammo State
            if (_data != null)
                CurrentAmmo = MagCapacity;

            OnAmmoChanged?.Invoke(CurrentAmmo, MagCapacity);

            // Begin holstered at scene start. Apply the Holstered Rigidbody/grab
            // settings explicitly first because CurrentState is already Holstered
            // (default field initializer) and EnterState(Holstered) would early-return
            // without running the case body. Without this, the prefab's Inspector
            // Rigidbody settings remain live — a non-kinematic Rigidbody would let
            // gravity pull the weapon through the floor at PIE start.
            _rigidbody.isKinematic = true;
            _grabInteractable.enabled = true;
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
            OnEquipChanged?.Invoke(true);
            OnAmmoChanged?.Invoke(CurrentAmmo, MagCapacity);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            // Block release during pause — weapon stays locked in hand.
            if (_pauseFrozen) return;

            if (CurrentState == WeaponInstanceState.Held ||
                CurrentState == WeaponInstanceState.Firing ||
                CurrentState == WeaponInstanceState.Reloading ||
                CurrentState == WeaponInstanceState.SlideBack)
            {
                UnsubscribeInputActions();
                OnEquipChanged?.Invoke(false);
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

            // Rate-limit both live fire and dry fire to prevent spam from
            // Value/Axis trigger actions that emit multiple performed events
            // per physical pull.
            float timeSinceLastShot = Time.time - _lastShotTime;
            if (_data != null && timeSinceLastShot < _data.FireInterval) return;

            if (CurrentAmmo <= 0)
            {
                // Dry-fire (GDD Rule 8) — haptic handled by S1-010; audio posted here (S1-006)
                _lastShotTime = Time.time;
                _audioService?.PostFeedbackEvent(new FeedbackEventData(
                    FeedbackEvent.WeaponDryFire,
                    _muzzleTransform != null ? _muzzleTransform.position : transform.position,
                    0f,
                    _heldInRightHand ? FeedbackHand.Right : FeedbackHand.Left));
                return;
            }

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

            // Slide rack: 3D audio at mag-well position + haptic (GDD row 5, S2-001)
            _audioService?.PostFeedbackEvent(new FeedbackEventData(
                eventType: FeedbackEvent.SlideRack,
                position:  _magWellTransform != null ? _magWellTransform.position : transform.position,
                magnitude: 0f,
                hand:      _heldInRightHand ? FeedbackHand.Right : FeedbackHand.Left));

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
            OnAmmoChanged?.Invoke(CurrentAmmo, MagCapacity);

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

            // Audio: 3D gunshot at muzzle position (S1-006)
            if (_muzzleTransform != null)
            {
                _audioService?.PostFeedbackEvent(new FeedbackEventData(
                    FeedbackEvent.WeaponFire,
                    _muzzleTransform.position,
                    _data != null ? _data.BaseDamage : 0f,
                    _heldInRightHand ? FeedbackHand.Right : FeedbackHand.Left));
            }

            // Muzzle flash VFX — spawned BEFORE ApplyRecoilKick() so position/rotation
            // are captured from the pre-recoil muzzle transform (GDD Rule 15). S1-007.
            if (_muzzleTransform != null)
            {
                _muzzleFlashPool?.Spawn(_muzzleTransform.position, _muzzleTransform.rotation);
            }

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

            // Magazine ejected — ammo is 0 until CompleteReload refills it.
            CurrentAmmo = 0;
            OnAmmoChanged?.Invoke(CurrentAmmo, MagCapacity);

            // Drop current magazine with physics via pool (GDD Rule 11 — no Instantiate/Destroy
            // in the combat loop). MagDropPool is resolved at Awake; null-safe if absent.
            if (_magDropPool != null && _magWellTransform != null && _data != null)
            {
                _magDropPool.Eject(
                    _magWellTransform.position,
                    _magWellTransform.rotation,
                    _data.MagazinePersistSeconds);
            }
            else if (_magDropPool == null)
            {
                Debug.LogWarning(
                    "[WeaponInstance] MagDropPool not resolved — magazine will not drop with physics. " +
                    "Ensure a MagDropPool component is present on the _Systems GO. S1-009.",
                    this);
            }

            // Hide the pistol's magazine mesh (visual feedback that the mag was ejected).
            if (_magMeshRenderer != null)
                _magMeshRenderer.enabled = false;

            // MagDrop audio (3D positional, no haptic per GDD row 3). S1-009.
            _audioService?.PostFeedbackEvent(new FeedbackEventData(
                eventType: FeedbackEvent.MagDrop,
                position:  _magWellTransform != null ? _magWellTransform.position : transform.position,
                magnitude: 0f,
                hand:      FeedbackHand.None));

            // Spawn fresh magazine on off-hand controller after _data.MagSpawnDelay seconds
            // (S2-001). Uses the off-hand controller transform so the mag appears held in
            // the player's other hand, not floating at a hip holster.
            // Null-safe — skipped if pool or rig are absent.
            if (_magSpawnPool != null && _rigControllerProvider != null && _data != null)
            {
                Transform offHand = _heldInRightHand
                    ? _rigControllerProvider.LeftControllerTransform
                    : _rigControllerProvider.RightControllerTransform;

                _magSpawnPool.Spawn(offHand, _data.MagSpawnDelay, MagCapacity);

                // Audio: off-hand magazine materialises (2D, no haptic per GDD row 4). S2-001.
                _audioService?.PostFeedbackEvent(new FeedbackEventData(
                    eventType: FeedbackEvent.MagSpawn,
                    position:  transform.position,
                    magnitude: 0f,
                    hand:      FeedbackHand.None));
            }

            // Enable the mag-well socket proximity check — it will call CompleteReload
            // when the player brings the magazine to the mag well.
            if (_magWellSocket != null)
                _magWellSocket.enabled = true;
        }

        /// <summary>
        /// Called by <see cref="MagWellSocket"/> (via <see cref="IMagInsertReceiver"/>)
        /// when the off-hand magazine enters the mag-well proximity radius.
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

            CurrentAmmo = MagCapacity;
            OnAmmoChanged?.Invoke(CurrentAmmo, MagCapacity);

            // Re-enable the pistol's magazine mesh (visual: mag is back in the weapon).
            if (_magMeshRenderer != null)
                _magMeshRenderer.enabled = true;

            // Magazine insertion: click SFX + haptic pulse (GDD row 6, S2-001).
            _audioService?.PostFeedbackEvent(new FeedbackEventData(
                eventType: FeedbackEvent.MagInsertion,
                position:  _magWellTransform != null ? _magWellTransform.position : transform.position,
                magnitude: 0f,
                hand:      _heldInRightHand ? FeedbackHand.Right : FeedbackHand.Left));

            // Disable the proximity socket — no longer needed until the next reload.
            if (_magWellSocket != null)
                _magWellSocket.enabled = false;

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

            // Holster click audio (2D, no haptic per GDD row 8). S1-009.
            _audioService?.PostFeedbackEvent(new FeedbackEventData(
                eventType: FeedbackEvent.WeaponHolster,
                position:  transform.position,
                magnitude: 0f,
                hand:      FeedbackHand.None));

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
            if (newRigState != RigState.Active)
            {
                // Pause/death: unsubscribe input and set frozen flag.
                // Do NOT disable XRGrabInteractable — that causes XRI to
                // force-release the grab, dropping the weapon.
                _pauseFrozen = true;
                UnsubscribeInputActions();
            }
            else
            {
                // Returning to Active: clear frozen flag and re-subscribe
                // input if weapon is still held.
                _pauseFrozen = false;

                if (CurrentState == WeaponInstanceState.Held ||
                    CurrentState == WeaponInstanceState.Firing ||
                    CurrentState == WeaponInstanceState.Reloading ||
                    CurrentState == WeaponInstanceState.SlideBack)
                {
                    SubscribeInputActions();
                }
            }
        }

        private bool IsRigActive()
        {
            return _rigStateProvider == null ||
                   _rigStateProvider.CurrentState == RigState.Active;
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
        // Generation seam
        // ===================================================================

        /// <summary>
        /// Read-only access to the current <see cref="WeaponData"/> instance.
        /// Used by <see cref="Presentation.HUD.HUDSystem"/> to populate the
        /// HUD-06 weapon stat panel (Block B) when the weapon is equipped.
        /// May be null if no data has been assigned yet.
        /// </summary>
        public WeaponData Data => _data;

        /// <summary>
        /// Called by <see cref="WeaponGeneration.WeaponSpawner"/> immediately after
        /// prefab instantiation to stamp in the procedurally generated
        /// <see cref="WeaponData"/> before <see cref="Awake"/> reads from it.
        ///
        /// <para>Must be called before this GameObject's <c>Awake</c> runs. If
        /// called after Awake, ammo is re-synced from the new data immediately.</para>
        /// </summary>
        internal void InjectGeneratedData(WeaponData generatedData)
        {
            if (generatedData == null)
            {
                Debug.LogError(
                    "[WeaponInstance] InjectGeneratedData called with null WeaponData — ignored.",
                    this);
                return;
            }

            _data = generatedData;

            // Always resync CurrentAmmo to the new MagCapacity. A freshly-generated
            // weapon spawns with a full mag per GDD §Ammo State. If Awake already
            // ran with the prefab's baked data, CurrentAmmo holds the OLD MagCapacity
            // and would otherwise stay stale (e.g., baked 12 vs rolled 18).
            CurrentAmmo = MagCapacity;
            OnAmmoChanged?.Invoke(CurrentAmmo, MagCapacity);
        }

        private int GetEffectiveMagCapacity()
        {
            Component generatedWeaponManager = GetComponent("GeneratedWeaponManager");
            if (generatedWeaponManager != null)
            {
                System.Type managerType = generatedWeaponManager.GetType();
                System.Reflection.PropertyInfo capacityProperty = managerType.GetProperty("CurrentMagazineCapacity");
                if (capacityProperty != null && capacityProperty.GetValue(generatedWeaponManager) is int generatedCapacity)
                {
                    return generatedCapacity;
                }
            }

            return _data != null ? _data.MagCapacity : 0;
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

            if (_magSpawnPool == null)
                Debug.LogWarning(
                    "[WeaponInstance] MagSpawnPool not resolved — off-hand magazine will not spawn. " +
                    "Ensure a MagSpawnPool component is present on the _Systems GO. S2-001.",
                    this);
        }
    }
}
