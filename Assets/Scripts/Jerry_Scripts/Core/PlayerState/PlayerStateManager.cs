using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JerryScripts.Core.PlayerState
{
        /// <summary>
        /// Runtime MonoBehaviour that owns player health, currency, and
        /// gameplay-lifecycle state. Implements both <see cref="IPlayerStateReader"/>
        /// (observable) and <see cref="IPlayerStateWriter"/> (mutating).
        ///
        /// <para><b>Wiring:</b> Attach to the <c>_Systems</c> GameObject in the scene.
        /// Assign a <see cref="PlayerStateConfig"/> asset. All consumers should inject
        /// either <see cref="IPlayerStateReader"/> or <see cref="IPlayerStateWriter"/> via
        /// the Inspector — never call <c>FindObjectOfType</c>.</para>
        ///
        /// <para><b>HP flow:</b> callers invoke <see cref="ApplyDamage"/> directly
        /// (e.g. via <c>PlayerHitboxReceiver</c>). This manager clamps the result to
        /// <c>[0, MaxHealth]</c> before storing and firing events.</para>
        ///
        /// <para><b>Death flow:</b> when <see cref="CurrentHealth"/> reaches zero
        /// <see cref="OnDeathConfirmed"/> fires first, then <see cref="OnStateChanged"/>
        /// fires with <see cref="PlayerState.Dead"/>.</para>
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

                [Header("Restart Target")]
                [Tooltip("Scene name to load on RequestRestart. Leave EMPTY to reload the current scene " +
                         "(legacy behavior). Set to your start-menu scene name (e.g. 'StartMenu') so " +
                         "death/restart returns the player to the tutorial.")]
                [SerializeField] private string _restartSceneName = "";

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
                        Time.timeScale = 1f;

                        if (_config == null)
                                Debug.LogWarning("[PlayerStateManager] No PlayerStateConfig assigned — using defaults (MaxHealth=100, StartingCurrency=0).", this);

                        InitializeState();
                }

                // ===================================================================
                // IPlayerStateWriter
                // ===================================================================

                /// <inheritdoc/>
                public void ApplyDamage(float amount)
                {
                        if (CurrentState == PlayerState.Dead) return;
                        if (float.IsNaN(amount))
                        {
                                Debug.LogWarning("[PlayerStateManager] ApplyDamage received NaN — ignored.", this);
                                return;
                        }
                        if (amount <= 0f) return;

                        SetHealth(CurrentHealth - amount);
                }

                /// <inheritdoc/>
                public void AddCurrency(int amount)
                {
                        if (amount <= 0) return; // Negative or zero amounts are silently ignored.

                        CurrentCurrency += amount;
                        OnCurrencyChanged?.Invoke(CurrentCurrency);
                }

                /// <inheritdoc/>
                public void ApplyHeal(float amount)
                {
                        if (CurrentState == PlayerState.Dead) return;
                        if (float.IsNaN(amount))
                        {
                                Debug.LogWarning("[PlayerStateManager] ApplyHeal received NaN — ignored.", this);
                                return;
                        }
                        if (amount <= 0f) return;

                        float newHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
                        if (newHealth == CurrentHealth) return;  // already at max

                        // SetHealth clamps to [0, MaxHealth] and fires OnHealthChanged on any
                        // actual change — it is not downward-only. Calling it with a higher value
                        // is the correct path for healing.
                        SetHealth(newHealth);
                }

                /// <inheritdoc/>
                public bool SpendCurrency(int amount)
                {
                        if (amount <= 0) return false;
                        if (CurrentCurrency < amount) return false;

                        CurrentCurrency -= amount;
                        OnCurrencyChanged?.Invoke(CurrentCurrency);
                        return true;
                }

                /// <inheritdoc/>
                public void RequestRestart()
                {
                        // Always restore time scale before doing anything else — death freezes the world
                        // via Time.timeScale = 0 (TriggerDeath); restart needs unscaled time to work.
                        Time.timeScale = 1f;

                        if (CurrentState == PlayerState.Running) return;

                        // EditMode tests run with Application.isPlaying == false; SceneManager.LoadScene
                        // is PlayMode-only and throws otherwise. Soft re-init exercises the same
                        // state-reset path Awake runs in a freshly loaded scene.
                        if (!Application.isPlaying)
                        {
                                InitializeState();
                                return;
                        }

                        // Full scene reload — resets all GameObjects, pools, and state cleanly.
                        string targetScene = string.IsNullOrEmpty(_restartSceneName)
                                ? SceneManager.GetActiveScene().name
                                : _restartSceneName;
                        SceneManager.LoadScene(targetScene);
                }

                /// <inheritdoc/>
                public void RequestPause()
                {
                        if (CurrentState != PlayerState.Running) return;
                        TransitionTo(PlayerState.Paused);

                        // Freeze the world — same pattern as TriggerDeath. Menu input via
                        // InputActionReference is unaffected (new Input System uses unscaled time).
                        // Restored to 1.0 in RequestResume / RequestRestart / RequestQuit.
                        Time.timeScale = 0f;
                }

                /// <inheritdoc/>
                public void RequestResume()
                {
                        // Always restore time scale before flipping state — even a no-op call
                        // (resume from non-Paused) cleans up any leftover freeze.
                        Time.timeScale = 1f;

                        if (CurrentState != PlayerState.Paused) return;
                        TransitionTo(PlayerState.Running);
                }

                /// <inheritdoc/>
                public void RequestQuit()
                {
                        Time.timeScale = 1f;
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
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
                /// Fires the death sequence: <see cref="OnDeathConfirmed"/>, posts
                /// <see cref="JerryScripts.Foundation.Audio.FeedbackEvent.PlayerDeath"/> audio,
                /// then transitions to <see cref="PlayerState.Dead"/> and fires
                /// <see cref="OnStateChanged"/>.
                /// </summary>
                private void TriggerDeath()
                {
                        OnDeathConfirmed?.Invoke();

                        var audioService = FindAnyObjectByType<JerryScripts.Foundation.Audio.AudioFeedbackService>();
                        audioService?.PostFeedbackEvent(new JerryScripts.Foundation.Audio.FeedbackEventData(
                                JerryScripts.Foundation.Audio.FeedbackEvent.PlayerDeath,
                                transform.position,
                                0f,
                                JerryScripts.Foundation.Audio.FeedbackHand.None));

                        TransitionTo(PlayerState.Dead);

                        // Freeze the world — stops enemy AI Update + WaitForSeconds coroutines,
                        // physics (bullets, AOE ticks), and player movement. Menu input via
                        // InputActionReference is unaffected (new Input System uses unscaled time).
                        // Restored to 1.0 in RequestRestart / RequestQuit.
                        Time.timeScale = 0f;
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
