using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace JerryScripts.Foundation
{
    /// <summary>
    /// Root MonoBehaviour for the player rig. Owns the rig FSM, exposes
    /// controller transforms and haptic players, and provides all six mount
    /// points used by weapon and holster systems.
    ///
    /// Implements: <see cref="IRigStateProvider"/>, <see cref="IRigControllerProvider"/>,
    /// <see cref="IMountPointProvider"/>.
    ///
    /// Attach to the XR Origin root GameObject. Wire all serialised fields via
    /// the inspector; nothing is looked up at runtime with Find().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRig : MonoBehaviour,
        IRigStateProvider,
        IRigControllerProvider,
        IMountPointProvider
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Config")]
        [Tooltip("ScriptableObject containing all tuning knobs for this rig.")]
        [SerializeField] private PlayerRigConfig _config;

        [Header("XR Origin References")]
        [Tooltip("The XROrigin component on this or a child GameObject.")]
        [SerializeField] private XROrigin _xrOrigin;

        [Tooltip("Camera object that represents the player head (inside the XR Rig camera offset).")]
        [SerializeField] private Transform _headTransform;

        [Header("Controller Transforms")]
        [SerializeField] private Transform _leftControllerTransform;
        [SerializeField] private Transform _rightControllerTransform;

        [Header("Haptic Players")]
        [Tooltip("HapticImpulsePlayer component on the left controller GameObject.")]
        [SerializeField] private HapticImpulsePlayer _leftHaptics;

        [Tooltip("HapticImpulsePlayer component on the right controller GameObject.")]
        [SerializeField] private HapticImpulsePlayer _rightHaptics;

        [Header("Mount Points")]
        [SerializeField] private Transform _rightHandMountPoint;
        [SerializeField] private Transform _leftHandMountPoint;
        [SerializeField] private Transform _rightHipHolster;
        [SerializeField] private Transform _leftHipHolster;
        [SerializeField] private Transform _backHolster;
        [SerializeField] private Transform _chestMountPoint;

        [Header("Locomotion")]
        [Tooltip("ContinuousMoveProvider in the rig hierarchy (optional — leave null to skip speed sync).")]
        [SerializeField] private ContinuousMoveProvider _continuousMoveProvider;

        [Tooltip("SnapTurnProvider in the rig hierarchy (optional — leave null to skip angle sync).")]
        [SerializeField] private SnapTurnProvider _snapTurnProvider;

        [Header("Pause Input")]
        [Tooltip("InputActionReference that maps to the pause gesture (e.g. menu button press).")]
        [SerializeField] private InputActionReference _pauseAction;

        // ===================================================================
        // IRigStateProvider
        // ===================================================================

        /// <inheritdoc/>
        public RigState CurrentState { get; private set; } = RigState.Initializing;

        /// <inheritdoc/>
        public event Action<RigState> OnStateChanged;

        /// <inheritdoc/>
        public event Action OnRigReady;

        /// <inheritdoc/>
        public event Action OnRigDeactivated;

        // ===================================================================
        // Damage reception — S1-008
        // ===================================================================

        /// <summary>
        /// Fired when the player's hitbox receives damage from an enemy source.
        /// Passes the resolved <c>FinalDamage</c> float (already clamped by the
        /// <see cref="JerryScripts.Foundation.Damage.IDamageResolver"/>).
        ///
        /// <para>Subscribe here (not on <see cref="PlayerHitbox"/>) for rig-level
        /// consumers such as a health system, death trigger, or screen-space VFX.
        /// Sam's Enemy System and any HUD overlay should subscribe to this event.</para>
        ///
        /// <para>The event is forwarded from <see cref="PlayerHitbox.OnDamageReceived"/>
        /// which is connected in <see cref="ConnectHitboxRelay"/>. Player-sourced damage
        /// is already filtered out before this event fires.</para>
        /// </summary>
        /// <remarks>S1-008. GDD: player-rig.md §Damage Reception.</remarks>
        public event Action<float> OnDamageReceived;

        // ===================================================================
        // IRigControllerProvider
        // ===================================================================

        /// <inheritdoc/>
        public Transform LeftControllerTransform => _leftControllerTransform;

        /// <inheritdoc/>
        public Transform RightControllerTransform => _rightControllerTransform;

        /// <inheritdoc/>
        public HapticImpulsePlayer LeftHaptics => _leftHaptics;

        /// <inheritdoc/>
        public HapticImpulsePlayer RightHaptics => _rightHaptics;

        // ===================================================================
        // IMountPointProvider
        // ===================================================================

        /// <inheritdoc/>
        public Transform RightHandMountPoint => _rightHandMountPoint;

        /// <inheritdoc/>
        public Transform LeftHandMountPoint => _leftHandMountPoint;

        /// <inheritdoc/>
        public Transform RightHipHolster => _rightHipHolster;

        /// <inheritdoc/>
        public Transform LeftHipHolster => _leftHipHolster;

        /// <inheritdoc/>
        public Transform BackHolster => _backHolster;

        /// <inheritdoc/>
        public Transform ChestMountPoint => _chestMountPoint;

        // ===================================================================
        // Private state
        // ===================================================================

        private readonly List<XRInputSubsystem> _inputSubsystems = new List<XRInputSubsystem>();
        private bool _trackingOriginReceived;
        private Coroutine _trackingConfirmCoroutine;
        private CapsuleCollider _hitboxCollider;
        private bool _rigReadyFired;

        // ===================================================================
        // Unity lifecycle
        // ===================================================================

        private void Awake()
        {
            ValidateReferences();
            SetupDamageCollider();
            SyncLocomotionConfig();
        }

        private void OnEnable()
        {
            // Subscribe to pause input
            if (_pauseAction != null)
            {
                _pauseAction.action.Enable();
                _pauseAction.action.performed += OnPausePerformed;
            }

            // Subscribe to XR tracking origin updates as the signal that
            // the headset has begun emitting valid tracking data.
            SubsystemManager.GetSubsystems(_inputSubsystems);
            foreach (var subsystem in _inputSubsystems)
                subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;

            // Begin the initializing-to-active transition check.
            _trackingConfirmCoroutine = StartCoroutine(AwaitTrackingConfirmed());
        }

        private void OnDisable()
        {
            if (_pauseAction != null)
                _pauseAction.action.performed -= OnPausePerformed;

            foreach (var subsystem in _inputSubsystems)
                subsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;

            if (_trackingConfirmCoroutine != null)
            {
                StopCoroutine(_trackingConfirmCoroutine);
                _trackingConfirmCoroutine = null;
            }
        }

        // ===================================================================
        // FSM transitions
        // ===================================================================

        /// <summary>
        /// Transitions the rig to the given state. No-ops if already in that state.
        /// Fires <see cref="OnStateChanged"/> on every real transition.
        /// Fires <see cref="OnRigReady"/> the first time the rig enters <see cref="RigState.Active"/>.
        /// Fires <see cref="OnRigDeactivated"/> when the rig enters <see cref="RigState.Dead"/>.
        /// </summary>
        public void TransitionTo(RigState newState)
        {
            if (CurrentState == newState) return;

            CurrentState = newState;

            ApplyStateEffects(newState);
            OnStateChanged?.Invoke(newState);

            if (newState == RigState.Active && !_rigReadyFired)
            {
                _rigReadyFired = true;
                OnRigReady?.Invoke();
            }

            if (newState == RigState.Dead)
                OnRigDeactivated?.Invoke();
        }

        /// <summary>
        /// Called externally (e.g. by a health system) to kill the player rig.
        /// Transitions to <see cref="RigState.Dead"/> regardless of current state.
        /// </summary>
        public void Die()
        {
            if (CurrentState == RigState.Dead) return;
            TransitionTo(RigState.Dead);
        }

        private void ApplyStateEffects(RigState state)
        {
            switch (state)
            {
                case RigState.Active:
                    // Re-enable locomotion providers if they exist
                    if (_continuousMoveProvider != null) _continuousMoveProvider.enabled = true;
                    if (_snapTurnProvider != null)       _snapTurnProvider.enabled       = true;
                    break;

                case RigState.Paused:
                case RigState.Dead:
                    // Freeze locomotion so player cannot move during pause/death
                    if (_continuousMoveProvider != null) _continuousMoveProvider.enabled = false;
                    if (_snapTurnProvider != null)       _snapTurnProvider.enabled       = false;
                    break;

                case RigState.Initializing:
                    // Locomotion disabled until tracking is confirmed
                    if (_continuousMoveProvider != null) _continuousMoveProvider.enabled = false;
                    if (_snapTurnProvider != null)       _snapTurnProvider.enabled       = false;
                    break;

                case RigState.Transitioning:
                    // All input blocked during room transitions (Room Management system owns entry/exit).
                    if (_continuousMoveProvider != null) _continuousMoveProvider.enabled = false;
                    if (_snapTurnProvider != null)       _snapTurnProvider.enabled       = false;
                    break;
            }
        }

        // ===================================================================
        // Pause toggle
        // ===================================================================

        private void OnPausePerformed(InputAction.CallbackContext _)
        {
            switch (CurrentState)
            {
                case RigState.Active:
                    TransitionTo(RigState.Paused);
                    break;
                case RigState.Paused:
                    TransitionTo(RigState.Active);
                    break;
                // Dead / Initializing ignore pause input.
            }
        }

        // ===================================================================
        // Tracking confirmation
        // ===================================================================

        private void OnTrackingOriginUpdated(XRInputSubsystem _)
        {
            _trackingOriginReceived = true;
        }

        private IEnumerator AwaitTrackingConfirmed()
        {
            // Wait until we receive at least one trackingOriginUpdated callback
            // AND the configured stabilisation delay has elapsed.
            float elapsed = 0f;
            float requiredDelay = _config != null ? _config.TrackingConfirmDelay : 0.5f;

            while (!_trackingOriginReceived || elapsed < requiredDelay)
            {
                elapsed += Time.unscaledDeltaTime;

                // Re-query subsystems each frame; the headset may connect after Awake.
                if (!_trackingOriginReceived)
                {
                    SubsystemManager.GetSubsystems(_inputSubsystems);
                    foreach (var subsystem in _inputSubsystems)
                        subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
                }

                yield return null;
            }

            TransitionTo(RigState.Active);
            _trackingConfirmCoroutine = null;
        }

        // ===================================================================
        // Damage collider (PlayerHitbox layer)
        // ===================================================================

        private void SetupDamageCollider()
        {
            int hitboxLayer = LayerMask.NameToLayer("PlayerHitbox");
            if (hitboxLayer == -1)
            {
                Debug.LogWarning(
                    "[PlayerRig] The 'PlayerHitbox' layer is not defined in Project Settings > Tags & Layers. " +
                    "Damage detection will not work until the layer is added and this GameObject's layer is set.",
                    this);
            }

            // GDD Rule 14: the hitbox is anchored to the XR Origin root (this transform).
            // Room-scale head movement must NOT allow dodging hit detection.
            // A dedicated child GO is used so the layer assignment does not affect the XR Origin root layer.
            GameObject hitboxGO = new GameObject("PlayerHitboxCollider");
            hitboxGO.transform.SetParent(transform, false);
            hitboxGO.transform.localPosition = Vector3.zero;
            hitboxGO.transform.localRotation = Quaternion.identity;

            float radius  = _config != null ? _config.HitboxRadius  : 0.25f;
            float height  = _config != null ? _config.HitboxHeight  : 1.7f;
            float centerY = _config != null ? _config.HitboxCenterY : 0.85f;

            // GDD Rule 13: CapsuleCollider, radius 0.25 m, height 1.7 m, centre at Y +0.85 m.
            _hitboxCollider = hitboxGO.AddComponent<CapsuleCollider>();
            _hitboxCollider.isTrigger = true;
            _hitboxCollider.radius    = radius;
            _hitboxCollider.height    = height;
            _hitboxCollider.center    = new Vector3(0f, centerY, 0f);
            _hitboxCollider.direction = 1; // Y-axis aligned

            if (hitboxLayer != -1)
                hitboxGO.layer = hitboxLayer;

            // Add the IHittable relay component and subscribe to its event (S1-008).
            // PlayerHitbox.TakeDamage filters player-source events before firing;
            // we simply forward the resulting float to any rig-level subscribers.
            ConnectHitboxRelay(hitboxGO);
        }

        // ===================================================================
        // Hitbox relay — S1-008
        // ===================================================================

        /// <summary>
        /// Adds a <see cref="PlayerHitbox"/> component to <paramref name="hitboxGO"/>
        /// and wires its <see cref="PlayerHitbox.OnDamageReceived"/> event to
        /// <see cref="OnDamageReceived"/> on this rig.
        ///
        /// Called once from <see cref="SetupDamageCollider"/>; split into its own
        /// method so the connection logic is independently readable and testable.
        /// </summary>
        private void ConnectHitboxRelay(GameObject hitboxGO)
        {
            PlayerHitbox hitbox = hitboxGO.AddComponent<PlayerHitbox>();
            hitbox.OnDamageReceived += damage => OnDamageReceived?.Invoke(damage);
        }

        // ===================================================================
        // Locomotion config sync
        // ===================================================================

        private void SyncLocomotionConfig()
        {
            if (_config == null) return;

            if (_continuousMoveProvider != null)
                _continuousMoveProvider.moveSpeed = _config.MoveSpeed;

            if (_snapTurnProvider != null)
                _snapTurnProvider.turnAmount = _config.SnapTurnAngle;
        }

        // ===================================================================
        // Validation
        // ===================================================================

        private void ValidateReferences()
        {
            if (_config == null)
                Debug.LogWarning("[PlayerRig] No PlayerRigConfig assigned. Default values will be used.", this);
            if (_xrOrigin == null)
                Debug.LogError("[PlayerRig] XROrigin reference is missing.", this);
            if (_headTransform == null)
                Debug.LogWarning("[PlayerRig] HeadTransform not assigned — camera-relative features (e.g. world-space HUD) may not function correctly.", this);
            if (_leftControllerTransform == null || _rightControllerTransform == null)
                Debug.LogError("[PlayerRig] One or both controller transforms are not assigned.", this);
            if (_leftHaptics == null || _rightHaptics == null)
                Debug.LogWarning("[PlayerRig] One or both HapticImpulsePlayer references are missing — haptics will not work.", this);
        }
    }
}
