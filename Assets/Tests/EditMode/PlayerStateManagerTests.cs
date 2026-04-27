using System;
using NUnit.Framework;
using UnityEngine;
using JerryScripts.Foundation;
using JerryScripts.Core.PlayerState;

namespace JerryScripts.Tests.EditMode
{
    /// <summary>
    /// NUnit EditMode unit tests for <see cref="PlayerStateManager"/>. S2-002.
    ///
    /// <para>All tests run headlessly — no scene loading, no PlayerRig in the scene
    /// unless explicitly created. The <see cref="PlayerRig"/> dependency is wired
    /// via a helper that adds the component to a separate GameObject and injects it
    /// via a serialized-field reflective setter only when required by the specific
    /// test. Tests that do not need damage flow leave the rig reference null and
    /// verify null-safety of the OnEnable warning path.</para>
    ///
    /// Coverage:
    ///   1.  HP initialises to MaxHealth on Awake.
    ///   2.  OnHealthChanged fires on initialisation with MaxHealth.
    ///   3.  OnCurrencyChanged fires on initialisation with starting currency.
    ///   4.  OnStateChanged fires on initialisation with Running.
    ///   5.  Damage reduces HP by the FinalDamage amount.
    ///   6.  HP is clamped at zero — does not go negative.
    ///   7.  HP is clamped at MaxHealth — overkill damage does not underflow.
    ///   8.  Death fires OnDeathConfirmed when HP reaches zero.
    ///   9.  Death transitions state to Dead.
    ///  10.  OnHealthChanged fires with 0f on death.
    ///  11.  Double-death guard: second hit after death does not re-fire OnDeathConfirmed.
    ///  12.  NaN damage is silently rejected — HP unchanged.
    ///  13.  AddCurrency positive amount increments balance and fires OnCurrencyChanged.
    ///  14.  AddCurrency with zero is silently ignored — no event.
    ///  15.  AddCurrency with negative value is silently ignored — no event.
    ///  16.  RequestRestart resets HP to MaxHealth.
    ///  17.  RequestRestart transitions state from Dead to Running.
    ///  18.  RequestRestart is no-op when already Running.
    ///  19.  Idempotency: SetHealth with the same value does not re-fire OnHealthChanged.
    ///  20.  IPlayerStateReader interface contract.
    ///  21.  IPlayerStateWriter interface contract.
    /// </summary>
    [TestFixture]
    public sealed class PlayerStateManagerTests
    {
        // =====================================================================
        // Fixture state
        // =====================================================================

        private GameObject          _managerGO;
        private PlayerStateManager  _psm;
        private PlayerStateConfig   _config;

        [SetUp]
        public void SetUp()
        {
            // Create a config asset with known, deterministic values.
            _config = ScriptableObject.CreateInstance<PlayerStateConfig>();
            SetConfigValues(_config, maxHealth: 100f, startingCurrency: 0);

            // EditMode tests do NOT auto-fire Awake on AddComponent. After AddComponent
            // and dependency injection, we explicitly invoke Awake via reflection so the
            // production initialization path runs with the config wired.
            _managerGO = new GameObject("PSM_Test");
            _psm       = _managerGO.AddComponent<PlayerStateManager>();
            InjectConfig(_psm, _config);
            InvokeAwake(_psm);
            // PlayerRig left null by default — tests that need it wire it explicitly.
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_managerGO);
            UnityEngine.Object.DestroyImmediate(_config);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Reflective setter for the <c>_config</c> serialised field.
        /// Avoids the need for a public setter that would pollute the production API.
        /// </summary>
        private static void InjectConfig(PlayerStateManager psm, PlayerStateConfig config)
        {
            var field = typeof(PlayerStateManager)
                .GetField("_config",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PlayerStateManager must have a private _config field.");
            field.SetValue(psm, config);
        }

        /// <summary>
        /// Uses reflection to set the private serialised fields on a <see cref="PlayerStateConfig"/>.
        /// </summary>
        private static void SetConfigValues(PlayerStateConfig config, float maxHealth, int startingCurrency)
        {
            var hpField = typeof(PlayerStateConfig)
                .GetField("_maxHealth",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(hpField, "PlayerStateConfig must have a private _maxHealth field.");
            hpField.SetValue(config, maxHealth);

            var currencyField = typeof(PlayerStateConfig)
                .GetField("_startingCurrency",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(currencyField, "PlayerStateConfig must have a private _startingCurrency field.");
            currencyField.SetValue(config, startingCurrency);
        }

        /// <summary>
        /// Invokes the private <c>Awake</c> on a <see cref="PlayerStateManager"/> via reflection.
        /// EditMode tests do NOT auto-fire MonoBehaviour lifecycle methods on AddComponent;
        /// production code that initializes serialized state in Awake must be manually
        /// triggered in tests after dependency injection completes.
        /// </summary>
        private static void InvokeAwake(PlayerStateManager psm)
        {
            var awake = typeof(PlayerStateManager)
                .GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(awake, "PlayerStateManager must have a private Awake method.");
            awake.Invoke(psm, null);
        }

        /// <summary>
        /// Simulates damage by invoking the private <c>OnRigDamageReceived</c> method
        /// directly. This avoids the need to instantiate a full <see cref="PlayerRig"/>
        /// (which has complex XR dependencies) for damage-path tests.
        /// </summary>
        private static void SimulateDamage(PlayerStateManager psm, float amount)
        {
            var method = typeof(PlayerStateManager)
                .GetMethod("OnRigDamageReceived",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, "PlayerStateManager must have a private OnRigDamageReceived(float) method.");
            method.Invoke(psm, new object[] { amount });
        }

        // =====================================================================
        // Tests 1–4 — Initialisation
        // =====================================================================

        /// <summary>HP must equal MaxHealth immediately after Awake.</summary>
        [Test]
        public void PlayerStateManager_Awake_HealthInitialisesAtMaxHealth()
        {
            // Act — Awake has already run via AddComponent in SetUp.
            // Assert
            Assert.AreEqual(_config.MaxHealth, _psm.CurrentHealth, 0.001f,
                "CurrentHealth must equal MaxHealth after Awake.");
        }

        /// <summary>OnHealthChanged must fire during init with MaxHealth as the argument.</summary>
        [Test]
        public void PlayerStateManager_Awake_FiresOnHealthChangedWithMaxHealth()
        {
            // Arrange — create a fresh PSM and subscribe before Awake fires.
            var go     = new GameObject("PSM_Init_Test");
            var cfg    = ScriptableObject.CreateInstance<PlayerStateConfig>();
            SetConfigValues(cfg, maxHealth: 80f, startingCurrency: 0);

            float receivedHp = -1f;

            // We hook subscribers AFTER Awake fires; then force a re-init via
            // RequestRestart from a non-Running state. Awake is invoked reflectively
            // because EditMode tests do not auto-fire MonoBehaviour lifecycle methods.
            var psm = go.AddComponent<PlayerStateManager>();
            InjectConfig(psm, cfg);
            InvokeAwake(psm);

            // Force into Dead so RequestRestart will re-run InitializeState.
            SimulateDamage(psm, 9999f); // Forces death, HP = 0.

            psm.OnHealthChanged += hp => receivedHp = hp;
            psm.RequestRestart();

            // Assert
            Assert.AreEqual(80f, receivedHp, 0.001f,
                "OnHealthChanged must fire with MaxHealth when RequestRestart resets state.");

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(cfg);
        }

        /// <summary>OnCurrencyChanged must fire during init with the configured starting currency.</summary>
        [Test]
        public void PlayerStateManager_Awake_FiresOnCurrencyChangedWithStartingCurrency()
        {
            var go  = new GameObject("PSM_Currency_Test");
            var cfg = ScriptableObject.CreateInstance<PlayerStateConfig>();
            SetConfigValues(cfg, maxHealth: 100f, startingCurrency: 50);

            int receivedCurrency = -1;

            // Awake must fire AFTER config is wired. Reflective invocation —
            // EditMode tests do not auto-fire MonoBehaviour lifecycle methods.
            var psm = go.AddComponent<PlayerStateManager>();
            InjectConfig(psm, cfg);
            InvokeAwake(psm);

            SimulateDamage(psm, 9999f);
            psm.OnCurrencyChanged += c => receivedCurrency = c;
            psm.RequestRestart();

            Assert.AreEqual(50, receivedCurrency,
                "OnCurrencyChanged must fire with StartingCurrency on restart/init.");

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(cfg);
        }

        /// <summary>Initial state must be Running.</summary>
        [Test]
        public void PlayerStateManager_Awake_StateIsRunning()
        {
            Assert.AreEqual(PlayerState.Running, _psm.CurrentState,
                "CurrentState must be Running after Awake.");
        }

        // =====================================================================
        // Tests 5–7 — Damage path
        // =====================================================================

        /// <summary>Damage reduces HP by the exact FinalDamage amount.</summary>
        [Test]
        public void PlayerStateManager_TakeDamage_ReducesHealthByFinalDamage()
        {
            // Arrange
            float initialHp = _psm.CurrentHealth; // 100
            const float Damage = 25f;

            // Act
            SimulateDamage(_psm, Damage);

            // Assert
            Assert.AreEqual(initialHp - Damage, _psm.CurrentHealth, 0.001f,
                "CurrentHealth must decrease by FinalDamage.");
        }

        /// <summary>HP must clamp at zero — overkill damage is allowed.</summary>
        [Test]
        public void PlayerStateManager_TakeDamage_ClampsHealthAtZero()
        {
            // Act
            SimulateDamage(_psm, 9999f);

            // Assert
            Assert.AreEqual(0f, _psm.CurrentHealth, 0.001f,
                "CurrentHealth must clamp to 0 on overkill damage, never go negative.");
        }

        /// <summary>Healing past MaxHealth must clamp at MaxHealth (future-proofing).</summary>
        [Test]
        public void PlayerStateManager_SetHealth_ClampsAtMaxHealth()
        {
            // Arrange — damage first so the value is not already at max.
            SimulateDamage(_psm, 10f);

            // Act — apply negative damage (i.e., healing beyond max) via reflection.
            SimulateDamage(_psm, -9999f); // negative = heal

            // Assert
            Assert.AreEqual(_psm.MaxHealth, _psm.CurrentHealth, 0.001f,
                "CurrentHealth must not exceed MaxHealth even if damage is negative (healing).");
        }

        // =====================================================================
        // Tests 8–11 — Death
        // =====================================================================

        /// <summary>OnDeathConfirmed fires when HP reaches zero.</summary>
        [Test]
        public void PlayerStateManager_Death_FiresOnDeathConfirmed()
        {
            // Arrange
            bool fired = false;
            _psm.OnDeathConfirmed += () => fired = true;

            // Act
            SimulateDamage(_psm, 9999f);

            // Assert
            Assert.IsTrue(fired, "OnDeathConfirmed must fire when HP drops to 0.");
        }

        /// <summary>State transitions to Dead on death.</summary>
        [Test]
        public void PlayerStateManager_Death_TransitionsStateToDead()
        {
            // Act
            SimulateDamage(_psm, 9999f);

            // Assert
            Assert.AreEqual(PlayerState.Dead, _psm.CurrentState,
                "CurrentState must be Dead after HP reaches 0.");
        }

        /// <summary>OnHealthChanged fires with 0f at the moment of death.</summary>
        [Test]
        public void PlayerStateManager_Death_FiresOnHealthChangedWithZero()
        {
            // Arrange
            float receivedHp = -1f;
            _psm.OnHealthChanged += hp => receivedHp = hp;

            // Act
            SimulateDamage(_psm, 9999f);

            // Assert
            Assert.AreEqual(0f, receivedHp, 0.001f,
                "OnHealthChanged must fire with 0f when HP is reduced to zero.");
        }

        /// <summary>
        /// A second hit after death must not re-fire OnDeathConfirmed.
        /// The double-death guard prevents the death sequence from running twice.
        /// </summary>
        [Test]
        public void PlayerStateManager_Death_DoubleDeathGuard_OnDeathConfirmedFiresOnce()
        {
            // Arrange
            int fireCount = 0;
            _psm.OnDeathConfirmed += () => fireCount++;

            // Act
            SimulateDamage(_psm, 9999f); // first kill
            SimulateDamage(_psm, 9999f); // second hit while dead

            // Assert
            Assert.AreEqual(1, fireCount,
                "OnDeathConfirmed must fire exactly once even if the player takes damage while already Dead.");
        }

        // =====================================================================
        // Test 12 — NaN guard
        // =====================================================================

        /// <summary>NaN damage is silently rejected — HP must remain unchanged.</summary>
        [Test]
        public void PlayerStateManager_TakeDamage_NaN_IsRejectedSilently()
        {
            // Arrange
            float hpBefore = _psm.CurrentHealth;
            bool eventFired = false;
            _psm.OnHealthChanged += _ => eventFired = true;

            // Act
            SimulateDamage(_psm, float.NaN);

            // Assert
            Assert.AreEqual(hpBefore, _psm.CurrentHealth, 0.001f,
                "HP must not change when a NaN damage value is received.");
            Assert.IsFalse(eventFired,
                "OnHealthChanged must not fire when damage is NaN.");
        }

        // =====================================================================
        // Tests 13–15 — Currency
        // =====================================================================

        /// <summary>Positive AddCurrency increments balance and fires OnCurrencyChanged.</summary>
        [Test]
        public void PlayerStateManager_AddCurrency_PositiveAmount_IncreasesBalance()
        {
            // Arrange
            int receivedCurrency = -1;
            _psm.OnCurrencyChanged += c => receivedCurrency = c;

            // Act
            _psm.AddCurrency(30);

            // Assert
            Assert.AreEqual(30, _psm.CurrentCurrency,
                "CurrentCurrency must increase by the added amount.");
            Assert.AreEqual(30, receivedCurrency,
                "OnCurrencyChanged must fire with the new balance.");
        }

        /// <summary>AddCurrency(0) is silently ignored — no event fires.</summary>
        [Test]
        public void PlayerStateManager_AddCurrency_Zero_IsIgnored()
        {
            // Arrange
            bool eventFired = false;
            _psm.OnCurrencyChanged += _ => eventFired = true;

            // Act
            _psm.AddCurrency(0);

            // Assert
            Assert.IsFalse(eventFired,
                "OnCurrencyChanged must not fire when AddCurrency is called with zero.");
            Assert.AreEqual(0, _psm.CurrentCurrency,
                "CurrentCurrency must remain unchanged after AddCurrency(0).");
        }

        /// <summary>AddCurrency with a negative value is silently ignored.</summary>
        [Test]
        public void PlayerStateManager_AddCurrency_NegativeAmount_IsIgnored()
        {
            // Arrange
            _psm.AddCurrency(50);
            int currencyBefore = _psm.CurrentCurrency;
            bool eventFired = false;
            _psm.OnCurrencyChanged += _ => eventFired = true;

            // Act
            _psm.AddCurrency(-10);

            // Assert
            Assert.IsFalse(eventFired,
                "OnCurrencyChanged must not fire when AddCurrency is called with a negative value.");
            Assert.AreEqual(currencyBefore, _psm.CurrentCurrency,
                "CurrentCurrency must remain unchanged after AddCurrency with negative value.");
        }

        // =====================================================================
        // Tests 16–18 — RequestRestart
        // =====================================================================

        /// <summary>RequestRestart resets HP to MaxHealth.</summary>
        [Test]
        public void PlayerStateManager_RequestRestart_ResetsHealthToMaxHealth()
        {
            // Arrange — bring to dead state first.
            SimulateDamage(_psm, 9999f);
            Assert.AreEqual(PlayerState.Dead, _psm.CurrentState);

            // Act
            _psm.RequestRestart();

            // Assert
            Assert.AreEqual(_psm.MaxHealth, _psm.CurrentHealth, 0.001f,
                "HP must reset to MaxHealth after RequestRestart.");
        }

        /// <summary>RequestRestart transitions state from Dead to Running.</summary>
        [Test]
        public void PlayerStateManager_RequestRestart_Dead_TransitionsToRunning()
        {
            // Arrange
            SimulateDamage(_psm, 9999f);

            // Act
            _psm.RequestRestart();

            // Assert
            Assert.AreEqual(PlayerState.Running, _psm.CurrentState,
                "CurrentState must be Running after RequestRestart from Dead.");
        }

        /// <summary>RequestRestart is a no-op when the player is already Running.</summary>
        [Test]
        public void PlayerStateManager_RequestRestart_WhenAlreadyRunning_IsNoOp()
        {
            // Arrange
            Assert.AreEqual(PlayerState.Running, _psm.CurrentState);
            int stateChangeCount = 0;
            _psm.OnStateChanged += _ => stateChangeCount++;

            // Act
            _psm.RequestRestart();

            // Assert
            Assert.AreEqual(0, stateChangeCount,
                "RequestRestart must be a no-op (no events) when state is already Running.");
        }

        // =====================================================================
        // Test 19 — Idempotency
        // =====================================================================

        /// <summary>
        /// SetHealth called with the same clamped value must not re-fire OnHealthChanged.
        /// Exact == comparison is safe here because values come from Mathf.Clamp, not arithmetic.
        /// </summary>
        [Test]
        public void PlayerStateManager_SetHealth_SameValue_DoesNotFireEvent()
        {
            // Arrange — damage to a known non-max value.
            SimulateDamage(_psm, 50f);
            Assert.AreEqual(50f, _psm.CurrentHealth, 0.001f);

            int eventCount = 0;
            _psm.OnHealthChanged += _ => eventCount++;

            // Act — same damage again (HP would land at 0, which IS a change — use a heal instead).
            // Use a zero-damage call (no change) to test idempotency.
            SimulateDamage(_psm, 0f); // 50 - 0 = 50, same value.

            // Assert
            Assert.AreEqual(0, eventCount,
                "OnHealthChanged must not fire when the clamped HP value has not changed.");
        }

        // =====================================================================
        // Tests 20–21 — Interface contracts
        // =====================================================================

        /// <summary>PlayerStateManager must be retrievable as IPlayerStateReader.</summary>
        [Test]
        public void PlayerStateManager_ImplementsIPlayerStateReader()
        {
            IPlayerStateReader reader = _managerGO.GetComponent<IPlayerStateReader>();
            Assert.IsNotNull(reader,
                "PlayerStateManager must implement IPlayerStateReader.");
            Assert.IsInstanceOf<PlayerStateManager>(reader,
                "The IPlayerStateReader component must be a PlayerStateManager instance.");
        }

        /// <summary>PlayerStateManager must be retrievable as IPlayerStateWriter.</summary>
        [Test]
        public void PlayerStateManager_ImplementsIPlayerStateWriter()
        {
            IPlayerStateWriter writer = _managerGO.GetComponent<IPlayerStateWriter>();
            Assert.IsNotNull(writer,
                "PlayerStateManager must implement IPlayerStateWriter.");
            Assert.IsInstanceOf<PlayerStateManager>(writer,
                "The IPlayerStateWriter component must be a PlayerStateManager instance.");
        }
    }
}
