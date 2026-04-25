using System;
using NUnit.Framework;
using UnityEngine;
using JerryScripts.Foundation;
using JerryScripts.Foundation.Damage;

namespace JerryScripts.Tests.EditMode
{
    /// <summary>
    /// NUnit EditMode unit tests for <see cref="PlayerHitbox"/>. S1-008.
    ///
    /// <para>All tests run headlessly — no scene loading, no AudioFeedbackService in the
    /// scene. The audio post is implicitly verified as null-safe (no NullReferenceException
    /// when the service is absent). Contract tests use a spy delegate to observe events.</para>
    ///
    /// Coverage:
    ///   1. Enemy-source damage fires <see cref="PlayerHitbox.OnDamageReceived"/> with the
    ///      correct <c>FinalDamage</c> value.
    ///   2. Player-source damage is silently ignored — <see cref="PlayerHitbox.OnDamageReceived"/>
    ///      does NOT fire (defensive guard per class doc).
    ///   3. <see cref="PlayerHitbox.OnDamageReceived"/> passes <c>FinalDamage</c> verbatim
    ///      (no rounding, no mutation by the relay).
    ///   4. Multiple subscriptions to <see cref="PlayerHitbox.OnDamageReceived"/> all
    ///      receive the event (standard C# multicast delegate contract).
    ///   5. <see cref="PlayerHitbox"/> implements <see cref="IHittable"/> (interface contract).
    /// </summary>
    [TestFixture]
    public sealed class PlayerHitboxTests
    {
        // =====================================================================
        // Fixture state
        // =====================================================================

        private GameObject  _go;
        private PlayerHitbox _hitbox;

        [SetUp]
        public void SetUp()
        {
            _go     = new GameObject("PlayerHitbox_Test");
            _hitbox = _go.AddComponent<PlayerHitbox>();
            // No AudioFeedbackService in scene — confirms null-safe audio path.
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Builds a <see cref="DamageEvent"/> with the given parameters.
        /// Keeps test bodies concise.
        /// </summary>
        private static DamageEvent MakeDamageEvent(float finalDamage, bool isPlayerSource)
            => new DamageEvent(finalDamage, "test-source", isPlayerSource, Vector3.zero);

        // =====================================================================
        // Test 1 — enemy-source damage fires OnDamageReceived
        // =====================================================================

        /// <summary>
        /// GDD §Damage Reception: an enemy-sourced <see cref="DamageEvent"/>
        /// (<c>IsPlayerSource == false</c>) must fire <see cref="PlayerHitbox.OnDamageReceived"/>.
        /// This is the primary acceptance criterion for S1-008 damage reception.
        /// </summary>
        [Test]
        public void PlayerHitbox_TakeDamage_EnemySource_FiresOnDamageReceived()
        {
            // Arrange
            bool eventFired = false;
            _hitbox.OnDamageReceived += _ => eventFired = true;
            DamageEvent dmg = MakeDamageEvent(finalDamage: 25f, isPlayerSource: false);

            // Act
            _hitbox.TakeDamage(dmg);

            // Assert
            Assert.IsTrue(eventFired,
                "OnDamageReceived must fire when the damage source is an enemy (IsPlayerSource=false).");
        }

        // =====================================================================
        // Test 2 — player-source damage is silently ignored
        // =====================================================================

        /// <summary>
        /// Defensive guard: a player-sourced <see cref="DamageEvent"/>
        /// (<c>IsPlayerSource == true</c>) must NOT fire
        /// <see cref="PlayerHitbox.OnDamageReceived"/>.
        ///
        /// <para>The hitscan system targets only the <c>EnemyHitbox</c> layer, so this
        /// guard will not normally be triggered. It is defensive against future routing
        /// patterns (analytics, observer systems) that might call any <see cref="IHittable"/>.</para>
        /// </summary>
        [Test]
        public void PlayerHitbox_TakeDamage_PlayerSource_SilentlyIgnored()
        {
            // Arrange
            bool eventFired = false;
            _hitbox.OnDamageReceived += _ => eventFired = true;
            DamageEvent dmg = MakeDamageEvent(finalDamage: 40f, isPlayerSource: true);

            // Act
            _hitbox.TakeDamage(dmg);

            // Assert
            Assert.IsFalse(eventFired,
                "OnDamageReceived must NOT fire when IsPlayerSource=true. " +
                "The PlayerHitbox must silently ignore player-sourced damage events.");
        }

        // =====================================================================
        // Test 3 — FinalDamage is passed verbatim
        // =====================================================================

        /// <summary>
        /// <see cref="PlayerHitbox.OnDamageReceived"/> must forward
        /// <see cref="DamageEvent.FinalDamage"/> without modification.
        /// The relay must not round, scale, or otherwise mutate the damage value;
        /// the subscriber (health system) applies its own logic.
        /// </summary>
        [Test]
        public void PlayerHitbox_TakeDamage_EnemySource_PassesFinalDamageVerbatim()
        {
            // Arrange
            const float ExpectedDamage = 87.5f;
            float       receivedDamage = -1f;
            _hitbox.OnDamageReceived += damage => receivedDamage = damage;
            DamageEvent dmg = MakeDamageEvent(finalDamage: ExpectedDamage, isPlayerSource: false);

            // Act
            _hitbox.TakeDamage(dmg);

            // Assert
            Assert.AreEqual(
                ExpectedDamage,
                receivedDamage,
                delta: 0.001f,
                "OnDamageReceived must pass FinalDamage verbatim — no rounding or scaling.");
        }

        // =====================================================================
        // Test 4 — multicast: all subscribers receive the event
        // =====================================================================

        /// <summary>
        /// Multiple subscribers to <see cref="PlayerHitbox.OnDamageReceived"/> must
        /// all receive the event. Verifies the standard C# multicast delegate contract
        /// (PlayerRig + health system + HUD can all subscribe independently).
        /// </summary>
        [Test]
        public void PlayerHitbox_TakeDamage_EnemySource_NotifiesAllSubscribers()
        {
            // Arrange
            int callCountA = 0;
            int callCountB = 0;
            _hitbox.OnDamageReceived += _ => callCountA++;
            _hitbox.OnDamageReceived += _ => callCountB++;
            DamageEvent dmg = MakeDamageEvent(finalDamage: 10f, isPlayerSource: false);

            // Act
            _hitbox.TakeDamage(dmg);

            // Assert
            Assert.AreEqual(1, callCountA, "First subscriber must receive exactly one event.");
            Assert.AreEqual(1, callCountB, "Second subscriber must receive exactly one event.");
        }

        // =====================================================================
        // Test 5 — IHittable interface contract
        // =====================================================================

        /// <summary>
        /// <see cref="PlayerHitbox"/> must implement <see cref="IHittable"/> so that
        /// Sam's Enemy System can call
        /// <c>GetComponent&lt;IHittable&gt;().TakeDamage(dmg)</c> without a direct
        /// reference to <see cref="PlayerHitbox"/>.
        /// </summary>
        [Test]
        public void PlayerHitbox_ImplementsIHittable()
        {
            // Act — query as interface (same path Sam's system uses)
            IHittable hittable = _go.GetComponent<IHittable>();

            // Assert
            Assert.IsNotNull(hittable,
                "PlayerHitbox must be retrievable as IHittable via GetComponent<IHittable>(). " +
                "Sam's Enemy System depends on this contract.");
            Assert.IsInstanceOf<PlayerHitbox>(hittable,
                "The IHittable component on the PlayerHitbox GO must be a PlayerHitbox instance.");
        }
    }
}
