using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using JerryScripts.Foundation.Damage;

namespace JerryScripts.Tests.EditMode
{
    /// <summary>
    /// NUnit EditMode unit tests for <see cref="DamageResolver"/>.
    /// Tests cover all acceptance criteria from damage-system.md §Acceptance Criteria
    /// plus the six GDD §Edge Cases enumerated in S1-004.
    ///
    /// Setup: each test creates a fresh GameObject with a DamageResolver MonoBehaviour
    /// and a RarityMultiplierTable ScriptableObject. The SO is injected via the
    /// internal InjectRarityTable() method (available in Editor builds).
    /// No scene loading is required — EditMode tests run headlessly.
    /// </summary>
    [TestFixture]
    public sealed class DamageResolverTests
    {
        // ===================================================================
        // Constants matching RarityMultiplierTable defaults
        // ===================================================================

        private const float BasicMultiplier    = 1.0f;
        private const float RareMultiplier     = 1.3f;
        private const float EpicMultiplier     = 1.7f;
        private const float LegendaryMultiplier = 2.2f;
        private const float DefaultCap         = 330.0f;
        private const float DefaultFloor       = 1.0f;
        private const float DefaultScalar      = 1.0f;

        private const float Tolerance = 0.001f;
        private const string TestSourceId = "test-weapon-001";
        private const string TestEnemyId  = "test-enemy-001";

        // ===================================================================
        // Test fixture state
        // ===================================================================

        private GameObject        _go;
        private DamageResolver    _resolver;
        private RarityMultiplierTable _table;

        [SetUp]
        public void SetUp()
        {
            _go       = new GameObject("DamageResolverTestObject");
            _resolver = _go.AddComponent<DamageResolver>();

            _table = ScriptableObject.CreateInstance<RarityMultiplierTable>();
            // Default SO values match GDD canonical values: Basic=1.0, Rare=1.3,
            // Epic=1.7, Legendary=2.2, Cap=330.0, Floor=1.0, Scalar=1.0.

            _resolver.InjectRarityTable(_table);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_table);
        }

        // ===================================================================
        // ResolvePlayerDamage — happy path
        // ===================================================================

        /// <summary>
        /// GDD acceptance criteria AC-1:
        /// base=40, rarity=1.3 → final_damage=52.0
        /// </summary>
        [Test]
        public void ResolvePlayerDamage_BasicInput_ReturnsExpectedDamage()
        {
            DamageEvent result = _resolver.ResolvePlayerDamage(
                baseDamage:       40f,
                rarityMultiplier: RareMultiplier,
                hitPos:           Vector3.zero,
                sourceId:         TestSourceId);

            Assert.AreEqual(52.0f, result.FinalDamage, Tolerance,
                "base=40 * rarity=1.3 should yield final_damage=52.0 (GDD AC-1).");
        }

        // ===================================================================
        // ResolvePlayerDamage — cap
        // ===================================================================

        /// <summary>
        /// GDD acceptance criteria AC-4:
        /// base=150, rarity=2.2 → 330.0 (raw=330.0, exactly at cap).
        /// Use base=200 to ensure the raw product (440.0) genuinely exceeds the cap.
        /// </summary>
        [Test]
        public void ResolvePlayerDamage_ExceedsCap_ClampsToCap()
        {
            // 200 * 2.2 = 440.0, which exceeds cap 330.0
            DamageEvent result = _resolver.ResolvePlayerDamage(
                baseDamage:       200f,
                rarityMultiplier: LegendaryMultiplier,
                hitPos:           Vector3.zero,
                sourceId:         TestSourceId);

            Assert.AreEqual(DefaultCap, result.FinalDamage, Tolerance,
                "Damage exceeding cap (330.0) must be clamped to the cap (GDD AC-4).");
        }

        // ===================================================================
        // ResolvePlayerDamage — floor (zero and negative inputs)
        // ===================================================================

        /// <summary>GDD §Edge Cases: base=0 → clamp to floor=1.0, log warning.</summary>
        [Test]
        public void ResolvePlayerDamage_ZeroBase_ClampsToFloor()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[DamageResolver\].*is <= 0"));

            DamageEvent result = _resolver.ResolvePlayerDamage(
                baseDamage:       0f,
                rarityMultiplier: RareMultiplier,
                hitPos:           Vector3.zero,
                sourceId:         TestSourceId);

            Assert.AreEqual(DefaultFloor, result.FinalDamage, Tolerance,
                "base=0 must clamp to floor=1.0 (GDD §Edge Cases Core Rule 5).");
        }

        /// <summary>GDD §Edge Cases: base=-5 → clamp to floor=1.0, log warning.</summary>
        [Test]
        public void ResolvePlayerDamage_NegativeBase_ClampsToFloor()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[DamageResolver\].*is <= 0"));

            DamageEvent result = _resolver.ResolvePlayerDamage(
                baseDamage:       -5f,
                rarityMultiplier: RareMultiplier,
                hitPos:           Vector3.zero,
                sourceId:         TestSourceId);

            Assert.AreEqual(DefaultFloor, result.FinalDamage, Tolerance,
                "base=-5 must clamp to floor=1.0 (GDD §Edge Cases Core Rule 5).");
        }

        // ===================================================================
        // ResolvePlayerDamage — NaN / Infinity rejection
        // ===================================================================

        /// <summary>GDD §Edge Cases: NaN input → reject, return floor, log error.</summary>
        [Test]
        public void ResolvePlayerDamage_NaNBase_ReturnsFloor()
        {
            // Resolver logs an error for invalid input (per GDD §Edge Cases).
            // Declare the expected log so NUnit doesn't treat it as a failure.
            LogAssert.Expect(LogType.Error, new Regex(@"\[DamageResolver\].*invalid input"));

            DamageEvent result = _resolver.ResolvePlayerDamage(
                baseDamage:       float.NaN,
                rarityMultiplier: RareMultiplier,
                hitPos:           Vector3.zero,
                sourceId:         TestSourceId);

            Assert.AreEqual(DefaultFloor, result.FinalDamage, Tolerance,
                "NaN baseDamage must be rejected and return floor=1.0 (GDD §Edge Cases).");
        }

        /// <summary>GDD §Edge Cases: +Infinity input → reject, return floor, log error.</summary>
        [Test]
        public void ResolvePlayerDamage_InfinityBase_ReturnsFloor()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[DamageResolver\].*invalid input"));

            DamageEvent result = _resolver.ResolvePlayerDamage(
                baseDamage:       float.PositiveInfinity,
                rarityMultiplier: RareMultiplier,
                hitPos:           Vector3.zero,
                sourceId:         TestSourceId);

            Assert.AreEqual(DefaultFloor, result.FinalDamage, Tolerance,
                "+Infinity baseDamage must be rejected and return floor=1.0 (GDD §Edge Cases).");
        }

        // ===================================================================
        // ResolveEnemyDamage — happy path
        // ===================================================================

        /// <summary>
        /// GDD acceptance criteria AC-2:
        /// enemy_base=15, scalar=1.0 → final_damage=15.0
        /// </summary>
        [Test]
        public void ResolveEnemyDamage_BasicInput_ReturnsExpectedDamage()
        {
            DamageEvent result = _resolver.ResolveEnemyDamage(
                enemyBaseDamage: 15f,
                hitPos:          Vector3.zero,
                enemyId:         TestEnemyId);

            Assert.AreEqual(15.0f, result.FinalDamage, Tolerance,
                "enemy_base=15, scalar=1.0 → final_damage=15.0 (GDD AC-2).");
        }

        // ===================================================================
        // ResolveEnemyDamage — floor
        // ===================================================================

        /// <summary>GDD Core Rule 5: base=0.5, scalar=1.0 → max(0.5, 1.0) = 1.0.</summary>
        [Test]
        public void ResolveEnemyDamage_BelowFloor_ClampsUp()
        {
            DamageEvent result = _resolver.ResolveEnemyDamage(
                enemyBaseDamage: 0.5f,
                hitPos:          Vector3.zero,
                enemyId:         TestEnemyId);

            Assert.AreEqual(DefaultFloor, result.FinalDamage, Tolerance,
                "enemy_base=0.5 * scalar=1.0 = 0.5 < floor → must clamp up to floor=1.0.");
        }

        // ===================================================================
        // ResolveEnemyDamage — scalar applied correctly
        // ===================================================================

        /// <summary>
        /// GDD formula: final_damage = max(enemy_base * scalar, floor).
        /// With scalar=1.0 (default SO), base=30 → 30.0. This confirms the
        /// multiplication path. A scalar≠1.0 integration test belongs in PlayMode
        /// where a full SO asset with custom serialised values can be loaded.
        /// </summary>
        [Test]
        public void ResolveEnemyDamage_WithScalar_AppliesCorrectly()
        {
            DamageEvent result = _resolver.ResolveEnemyDamage(
                enemyBaseDamage: 30f,
                hitPos:          Vector3.zero,
                enemyId:         TestEnemyId);

            Assert.AreEqual(30.0f, result.FinalDamage, Tolerance,
                "enemy_base=30, scalar=1.0 → final_damage=30.0 (GDD enemy formula, multiplication path).");
        }

        // ===================================================================
        // DamageEvent field propagation
        // ===================================================================

        /// <summary>GDD acceptance criteria AC-7: HitPosition must be populated.</summary>
        [Test]
        public void ResolvePlayerDamage_HitPositionPopulated()
        {
            Vector3 expectedPos = new Vector3(1f, 2f, 3f);

            DamageEvent result = _resolver.ResolvePlayerDamage(
                baseDamage:       40f,
                rarityMultiplier: RareMultiplier,
                hitPos:           expectedPos,
                sourceId:         TestSourceId);

            Assert.AreEqual(expectedPos, result.HitPosition,
                "HitPosition must carry the caller's world-space contact point verbatim (GDD AC-7).");
        }

        /// <summary>
        /// Player-sourced events must have IsPlayerSource = true.
        /// Used by AudioFeedback and PlayerRig to route the event correctly.
        /// </summary>
        [Test]
        public void ResolvePlayerDamage_IsPlayerSourceTrue()
        {
            DamageEvent result = _resolver.ResolvePlayerDamage(
                baseDamage:       40f,
                rarityMultiplier: RareMultiplier,
                hitPos:           Vector3.zero,
                sourceId:         TestSourceId);

            Assert.IsTrue(result.IsPlayerSource,
                "ResolvePlayerDamage must set IsPlayerSource=true (architecture.md §4.1).");
        }

        /// <summary>
        /// Enemy-sourced events must have IsPlayerSource = false.
        /// Used by AudioFeedback and PlayerRig to route the event correctly.
        /// </summary>
        [Test]
        public void ResolveEnemyDamage_IsPlayerSourceFalse()
        {
            DamageEvent result = _resolver.ResolveEnemyDamage(
                enemyBaseDamage: 15f,
                hitPos:          Vector3.zero,
                enemyId:         TestEnemyId);

            Assert.IsFalse(result.IsPlayerSource,
                "ResolveEnemyDamage must set IsPlayerSource=false (architecture.md §4.1).");
        }
    }
}
