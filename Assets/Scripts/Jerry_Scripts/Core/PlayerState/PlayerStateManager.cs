using System;
using JerryScripts.Foundation;
using UnityEngine;

namespace JerryScripts.Core.PlayerState
{
    /// <summary>
    /// Runtime MonoBehaviour that owns player health, currency, and
    /// gameplay-lifecycle state. Implements both <see cref="IPlayerStateReader"/>
    /// (observable) and <see cref="IPlayerStateWriter"/> (mutating).
    ///
    /// <para><b>Wiring:</b> Attach to the <c>_Systems</c> GameObject in the scene.
    /// Assign a <see cref="PlayerStateConfig"/> asset and drag in the scene's
    /// <see cref="PlayerRig"/> reference. All consumers should inject either
    /// <see cref="IPlayerStateReader"/> or <see cref="IPlayerStateWriter"/> via
    /// the Inspector — never call <c>FindObjectOfType</c>.</para>
    ///
    /// <para><b>HP flow:</b> subscribes to <see cref="PlayerRig.OnDamageReceived"/>
    /// in <c>OnEnable</c> and unsubscribes in <c>OnDisable</c>. The rig event
    /// carries the already-resolved <c>FinalDamage</c> float; this manager clamps
    /// the result to <c>[0, MaxHealth]</c> before storing and firing events.</para>
    ///
    /// <para><b>Death flow:</b> when <see cref="CurrentHealth"/> reaches zero
    /// <see cref="OnDeathConfirmed"/> fires first, then <see cref="PlayerRig.Die"/>
    /// is called on the wired rig, then <see cref="OnStateChanged"/> fires with
    /// <see cref="PlayerState.Dead"/>.</para>
    ///
    /// <para><b>Idempotency:</b> <c>SetHealth</c> does not fire events if the clamped
    /// value equals the current value (exact <c>==</c> comparison — no arithmetic
    /// involved in the assignment path).</para>
    /// </summary>
    /// <remarks>S2-002. GDD: player-state-management.md.</remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerStateManager : MonoBehaviour,
        IPlayerStateReader,
        IPlayerStateWriter
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Config")]
        [Tooltip("ScriptableObject with HP cap and starting currency.")]
        [SerializeField] private PlayerStateConfig _config;

        [Header("Rig Reference")]
        [Tooltip("The scene PlayerRig. PlayerStateManager subscribes to OnDamageReceived " +
                 "and calls Die() on death. Assign via Inspector — no Find() at runtime.")]
        [SerializeField] private PlayerRig _playerRig;

        // ===================================================================
        // IPlayerStateReader — snapshot properties
        // ===================================================================

        /// <inheritdoc/>
        public PlayerState CurrentState { get; private set; } = PlayerState.Running;

        /// <inheritdoc/>
        public float CurrentHealth { get; private set; }

        /// <inheritdoc/>
        public float MaxHealth => _config != null ? _config.MaxHealth : 100f;

        /// <inheritdoc/>
        public int CurrentCurrency { get; private set; }

        // ===================================================================
        // IPlayerStateReader — events
        // ===================================================================

        /// <inheritdoc/>
        public event Action<PlayerState> OnStateChanged;

        /// <inheritdoc/>
        public event Action<float> OnHealthChanged;

        /// <inheritdoc/>
        public event Action<int> OnCurrencyChanged;

        /// <inheritdoc/>
        public event Action OnDeathConfirmed;

        // ===================================================================
        // Unity lifecycle
        // ===================================================================

        private void Awake()
        {
            if (_config == null)
                Debug.LogWarning("[PlayerStateManager] No PlayerStateConfig assigned — using defaults (MaxHealth=100, StartingCurrency=0).", this);

            InitializeState();
        }

        private void OnEnable()
        {
            if (_playerRig != null)
                _playerRig.OnDamageReceived += OnRigDamageReceived;
            else
                Debug.LogWarning("[PlayerStateManager] PlayerRig reference is null — damage will not reduce HP. Assign via Inspector.", this);
        }

        private void OnDisable()
        {
            if (_playerRig != null)
                _playerRig.OnDamageReceived -= OnRigDamageReceived;
        }

        // ===================================================================
        // IPlayerStateWriter
        // ===================================================================

        /// <inheritdoc/>
        public void AddCurrency(int amount)
        {
            if (amount <= 0) return; // Negative or zero amounts are silently ignored.

            CurrentCurrency += amount;
            OnCurrencyChanged?.Invoke(CurrentCurrency);
        }

        /// <inheritdoc/>
        public void RequestRestart()
        {
            if (CurrentState == PlayerState.Running) return;

            InitializeState();
        }

        /// <inheritdoc/>
        public void RequestQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ===================================================================
        // Private — damage subscription handler
        // ===================================================================

        /// <summary>
        /// Receives the resolved <c>FinalDamage</c> float from <see cref="PlayerRig.OnDamageReceived"/>.
        /// Applies damage, clamps HP, and triggers death if HP reaches zero.
        /// No-op when the player is already dead.
        /// </summary>
        private void OnRigDamageReceived(float finalDamage)
        {
            // Ignore damage after death — prevents double-death from rapid hits.
            if (CurrentState == PlayerState.Dead) return;

            // Reject NaN — a corrupt damage value must not lock the player into
            // an undefined HP state. Log once and skip.
            if (float.IsNaN(finalDamage))
            {
                Debug.LogWarning("[PlayerStateManager] Received NaN damage value — ignoring.", this);
                return;
            }

            SetHealth(CurrentHealth - finalDamage);
        }

        // ===================================================================
        // Private — helpers
        // ===================================================================

        /// <summary>
        /// Sets <see cref="CurrentHealth"/> to the clamped value and fires
        /// <see cref="OnHealthChanged"/> only when the value actually changes.
        /// Triggers the death sequence when clamped HP == 0.
        /// </summary>
        private void SetHealth(float rawValue)
        {
            float clamped = Mathf.Clamp(rawValue, 0f, MaxHealth);

            // Exact == is correct here: CurrentHealth is only ever assigned from
            // Mathf.Clamp or MaxHealth (direct field assignment, no floating-point
            // arithmetic between reads). The idempotency guard avoids spurious events.
            if (CurrentHealth == clamped) return;

            CurrentHealth = clamped;
            OnHealthChanged?.Invoke(CurrentHealth);

            if (CurrentHealth == 0f)
                TriggerDeath();
        }

        /// <summary>
        /// Fires the death sequence: <see cref="OnDeathConfirmed"/>,
        /// calls <see cref="PlayerRig.Die"/> on the wired rig (null-safe),
        /// then transitions to <see cref="PlayerState.Dead"/> and fires
        /// <see cref="OnStateChanged"/>.
        /// </summary>
        private void TriggerDeath()
        {
            OnDeathConfirmed?.Invoke();

            _playerRig?.Die();

            TransitionTo(PlayerState.Dead);
        }

        /// <summary>
        /// Transitions to <paramref name="newState"/> if different from the current
        /// state. Fires <see cref="OnStateChanged"/> on real transitions only.
        /// </summary>
        private void TransitionTo(PlayerState newState)
        {
            if (CurrentState == newState) return;

            CurrentState = newState;
            OnStateChanged?.Invoke(CurrentState);
        }

        /// <summary>
        /// Sets HP to <see cref="MaxHealth"/> and currency to the configured starting
        /// value, then transitions to <see cref="PlayerState.Running"/>.
        /// Called from <see cref="Awake"/> and <see cref="RequestRestart"/>.
        ///
        /// <para>Events are fired in order: <see cref="OnHealthChanged"/>,
        /// <see cref="OnCurrencyChanged"/>, <see cref="OnStateChanged"/>.</para>
        /// </summary>
        private void InitializeState()
        {
            // Set HP directly — bypass SetHealth idempotency guard on first init
            // so OnHealthChanged always fires at startup even if MaxHealth == default(float).
            CurrentHealth = MaxHealth;
            OnHealthChanged?.Invoke(CurrentHealth);

            int startCurrency = _config != null ? _config.StartingCurrency : 0;
            CurrentCurrency = startCurrency;
            OnCurrencyChanged?.Invoke(CurrentCurrency);

            TransitionTo(PlayerState.Running);
        }
    }
}
