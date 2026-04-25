using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using JerryScripts.Foundation.Audio;

namespace JerryScripts.Tests.EditMode
{
    /// <summary>
    /// NUnit EditMode unit tests for the S1-006 Audio Feedback system.
    ///
    /// Coverage:
    ///   1. <see cref="AudioFeedbackService.CalculateHitConfirmationPitch"/> — pure static,
    ///      no scene required. GDD §Audio &amp; Feedback, §Formulas.
    ///   2. <see cref="ShuffleBag{T}"/> — no-repeat guarantee, cycle boundary, determinism.
    ///   3. Contract guards — interface shape, implementation guard, enum variant counts.
    ///
    /// Enum variant counts: FeedbackEvent has all 19 GDD-cataloged variants declared
    /// as a forward-compatibility contract (audio-feedback-system.md §Feedback Event Catalog).
    /// FeedbackHand has 4 values: None, Left, Right, Both.
    /// </summary>
    [TestFixture]
    public sealed class AudioFeedbackServiceTests
    {
        // =====================================================================
        // GDD defaults (must match FeedbackEventConfig inspector defaults)
        // =====================================================================

        private const float DamageCap      = 330f;
        private const float PitchFloor     = 0.9f;
        private const float PitchCeiling   = 1.4f;
        private const float Tolerance      = 0.001f;

        // =====================================================================
        // CalculateHitConfirmationPitch — pure formula tests
        // =====================================================================

        /// <summary>
        /// GDD §Formulas: damage=0 → t=InverseLerp(0, 330, 0)=0 → Lerp(0.9, 1.4, 0)=0.9.
        /// Zero damage must return the pitch floor exactly.
        /// </summary>
        [Test]
        public void CalculateHitConfirmationPitch_ZeroDamage_ReturnsFloor()
        {
            // Arrange / Act
            float result = AudioFeedbackService.CalculateHitConfirmationPitch(
                finalDamage:   0f,
                damageCap:     DamageCap,
                pitchFloor:    PitchFloor,
                pitchCeiling:  PitchCeiling);

            // Assert
            Assert.AreEqual(PitchFloor, result, Tolerance,
                "damage=0 must return pitchFloor=0.9 (GDD §Formulas, t=0 boundary).");
        }

        /// <summary>
        /// GDD §Formulas: damage=165 (half cap) →
        /// t=InverseLerp(0, 330, 165)=0.5 → Lerp(0.9, 1.4, 0.5)=1.15.
        /// Mid-range damage must interpolate to the midpoint pitch.
        /// </summary>
        [Test]
        public void CalculateHitConfirmationPitch_HalfCapDamage_ReturnsMidpointPitch()
        {
            // Arrange
            float halfCap        = DamageCap * 0.5f;   // 165
            float expectedPitch  = PitchFloor + (PitchCeiling - PitchFloor) * 0.5f;  // 1.15

            // Act
            float result = AudioFeedbackService.CalculateHitConfirmationPitch(
                finalDamage:   halfCap,
                damageCap:     DamageCap,
                pitchFloor:    PitchFloor,
                pitchCeiling:  PitchCeiling);

            // Assert
            Assert.AreEqual(expectedPitch, result, Tolerance,
                "damage=165 (half cap) must yield pitch=1.15 (GDD §Formulas, t=0.5 midpoint).");
        }

        /// <summary>
        /// GDD §Formulas: damage=330 (exactly at cap) →
        /// t=InverseLerp(0, 330, 330)=1 → Lerp(0.9, 1.4, 1)=1.4.
        /// Damage at cap must return pitch ceiling.
        /// </summary>
        [Test]
        public void CalculateHitConfirmationPitch_AtCapDamage_ReturnsCeiling()
        {
            // Act
            float result = AudioFeedbackService.CalculateHitConfirmationPitch(
                finalDamage:   DamageCap,
                damageCap:     DamageCap,
                pitchFloor:    PitchFloor,
                pitchCeiling:  PitchCeiling);

            // Assert
            Assert.AreEqual(PitchCeiling, result, Tolerance,
                "damage=330 (at cap) must return pitchCeiling=1.4 (GDD §Formulas, t=1 boundary).");
        }

        /// <summary>
        /// GDD §Edge Cases: damage above cap must clamp to ceiling.
        /// Mathf.Lerp clamps t — InverseLerp(0, 330, 500) returns 1.0 internally.
        /// </summary>
        [Test]
        public void CalculateHitConfirmationPitch_AboveCapDamage_ClampsToCeiling()
        {
            // Act
            float result = AudioFeedbackService.CalculateHitConfirmationPitch(
                finalDamage:   DamageCap + 200f,   // 530 — well above cap
                damageCap:     DamageCap,
                pitchFloor:    PitchFloor,
                pitchCeiling:  PitchCeiling);

            // Assert
            Assert.AreEqual(PitchCeiling, result, Tolerance,
                "damage above cap must clamp to pitchCeiling=1.4 (GDD §Edge Cases).");
        }

        // =====================================================================
        // ShuffleBag — single-item behaviour
        // =====================================================================

        /// <summary>
        /// A bag with one item must return that same item on every draw.
        /// No shuffle is needed for a single element — the result is deterministic.
        /// </summary>
        [Test]
        public void ShuffleBag_SingleItem_AlwaysReturnsSameItem()
        {
            // Arrange
            const string item = "only-clip";
            var bag = new ShuffleBag<string>(new[] { item }, new Random(0));

            // Act / Assert
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(item, bag.Next(),
                    $"Draw {i}: single-item bag must always return the same item.");
            }
        }

        // =====================================================================
        // ShuffleBag — no-repeat guarantee over two full cycles
        // =====================================================================

        /// <summary>
        /// A bag of 3 items drawn 6 times (two full cycles) must:
        ///   1. Return all 3 items in the first cycle (cycle 1: draws 0-2).
        ///   2. Return all 3 items in the second cycle (cycle 2: draws 3-5).
        ///   3. Not repeat immediately across the cycle boundary (last item of cycle 1
        ///      must differ from first item of cycle 2).
        ///
        /// Seeded PRNG makes the sequence deterministic so the no-immediate-repeat
        /// assertion is not probabilistic.
        /// </summary>
        [Test]
        public void ShuffleBag_ThreeItems_SixDraws_AllItemsInEachCycleNoImmediateRepeat()
        {
            // Arrange
            var items = new[] { "A", "B", "C" };

            // Use a fixed seed. Seed 42 produces a sequence where the last item of
            // cycle 1 differs from the first item of cycle 2.
            // If this assertion ever fails after an RNG-algorithm change, pick a
            // seed where last-of-cycle-1 != first-of-cycle-2 via a short search.
            var bag = new ShuffleBag<string>(items, new Random(42));

            // Act — draw two full cycles
            var cycle1 = new string[3];
            var cycle2 = new string[3];
            for (int i = 0; i < 3; i++) cycle1[i] = bag.Next();
            for (int i = 0; i < 3; i++) cycle2[i] = bag.Next();

            // Assert 1: cycle 1 contains all 3 items
            CollectionAssert.AreEquivalent(items, cycle1,
                "Cycle 1 must contain all 3 items exactly once (shuffle-bag no-repeat guarantee).");

            // Assert 2: cycle 2 contains all 3 items
            CollectionAssert.AreEquivalent(items, cycle2,
                "Cycle 2 must contain all 3 items exactly once (shuffle-bag no-repeat guarantee).");

            // Assert 3: no immediate repeat across cycle boundary
            string lastOfCycle1  = cycle1[2];
            string firstOfCycle2 = cycle2[0];
            Assert.AreNotEqual(lastOfCycle1, firstOfCycle2,
                $"Last item of cycle 1 ('{lastOfCycle1}') must differ from " +
                $"first item of cycle 2 ('{firstOfCycle2}') — immediate repeats across " +
                "cycle boundary are disallowed by the shuffle-bag design (S1-006 comment).");
        }

        // =====================================================================
        // ShuffleBag — determinism with seeded PRNG
        // =====================================================================

        /// <summary>
        /// Two bags built with the same seed and the same items must produce
        /// identical draw sequences. Proves the seeded constructor is deterministic
        /// and that tests relying on specific sequences are repeatable in CI.
        /// </summary>
        [Test]
        public void ShuffleBag_SameSeed_ProducesSameSequence()
        {
            // Arrange
            var items   = new[] { 10, 20, 30 };
            int seed    = 99;
            var bagA    = new ShuffleBag<int>(items, new Random(seed));
            var bagB    = new ShuffleBag<int>(items, new Random(seed));

            // Act / Assert — draw 6 items from each (two full cycles)
            for (int i = 0; i < 6; i++)
            {
                int a = bagA.Next();
                int b = bagB.Next();
                Assert.AreEqual(a, b,
                    $"Draw {i}: same-seeded bags must produce identical sequences.");
            }
        }

        // =====================================================================
        // Contract guard — IAudioFeedbackService interface shape
        // =====================================================================

        /// <summary>
        /// <see cref="IAudioFeedbackService"/> must expose exactly one public instance method
        /// named <c>PostFeedbackEvent</c>. Adding methods here is a breaking change to all
        /// consumers — this test catches accidental additions before CI does.
        /// </summary>
        [Test]
        public void IAudioFeedbackService_ExposesPostFeedbackEventOnly()
        {
            // Arrange
            MethodInfo[] methods = typeof(IAudioFeedbackService)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance);

            // Assert — count
            Assert.AreEqual(1, methods.Length,
                "IAudioFeedbackService must expose exactly 1 method. " +
                "Adding methods is a breaking change to all consumers.");

            // Assert — name
            Assert.AreEqual("PostFeedbackEvent", methods[0].Name,
                "The only method on IAudioFeedbackService must be PostFeedbackEvent.");
        }

        // =====================================================================
        // Contract guard — AudioFeedbackService implements the interface
        // =====================================================================

        /// <summary>
        /// <see cref="AudioFeedbackService"/> must implement <see cref="IAudioFeedbackService"/>.
        /// Compile-time guard expressed as a test so regressions surface in CI.
        /// </summary>
        [Test]
        public void AudioFeedbackService_ImplementsIAudioFeedbackService()
        {
            Assert.IsTrue(
                typeof(IAudioFeedbackService).IsAssignableFrom(typeof(AudioFeedbackService)),
                "AudioFeedbackService must implement IAudioFeedbackService.");
        }

        // =====================================================================
        // Contract guard — FeedbackEvent enum variant count
        // =====================================================================

        /// <summary>
        /// <see cref="FeedbackEvent"/> must declare all 19 variants from the GDD
        /// §Feedback Event Catalog. This is a forward-compatibility contract: future
        /// stories add config entries and clips without touching this enum.
        /// When a new event type is added to the GDD, update this count AND add the
        /// matching value to <c>FeedbackEventData.cs</c> and a
        /// <see cref="FeedbackEventConfig.EventEntry"/> in the config asset.
        /// </summary>
        [Test]
        public void FeedbackEvent_HasNineteenGDDVariants()
        {
            // Arrange
            int variantCount = Enum.GetValues(typeof(FeedbackEvent)).Length;

            // Assert
            Assert.AreEqual(19, variantCount,
                "FeedbackEvent must have all 19 GDD-cataloged variants. " +
                "Update GDD §Feedback Event Catalog if changing.");
        }

        // =====================================================================
        // Contract guard — FeedbackHand enum variant count
        // =====================================================================

        /// <summary>
        /// <see cref="FeedbackHand"/> must have exactly 4 variants: None, Left, Right, Both.
        /// The <c>Both</c> value supports two-handed interactions and future haptic routing.
        /// When a new hand value is added, update this count.
        /// </summary>
        [Test]
        public void FeedbackHand_HasFourVariants()
        {
            // Arrange
            int variantCount = Enum.GetValues(typeof(FeedbackHand)).Length;

            // Assert
            Assert.AreEqual(4, variantCount,
                "FeedbackHand must have None/Left/Right/Both.");
        }

        // =====================================================================
        // Contract guard — EventEntry haptic fields (S1-006)
        // =====================================================================

        /// <summary>
        /// <see cref="FeedbackEventConfig.EventEntry"/> must expose public fields
        /// <c>HapticAmplitude</c> and <c>HapticDuration</c> (both <c>float</c>).
        /// These fields are the canonical source of haptic parameters for
        /// <see cref="AudioFeedbackService.PostFeedbackEvent"/> — removing them
        /// breaks haptic dispatch without a compile error. S1-006 ownership transfer.
        /// </summary>
        [Test]
        public void FeedbackEventEntry_ExposesHapticAmplitudeAndDuration()
        {
            // Arrange
            var entryType = typeof(FeedbackEventConfig.EventEntry);

            // Act
            var amp = entryType.GetField("HapticAmplitude");
            var dur = entryType.GetField("HapticDuration");

            // Assert — fields exist
            Assert.IsNotNull(amp, "EventEntry.HapticAmplitude must exist (S1-006 haptic ownership).");
            Assert.IsNotNull(dur, "EventEntry.HapticDuration must exist (S1-006 haptic ownership).");

            // Assert — correct types
            Assert.AreEqual(typeof(float), amp.FieldType,
                "EventEntry.HapticAmplitude must be a float field.");
            Assert.AreEqual(typeof(float), dur.FieldType,
                "EventEntry.HapticDuration must be a float field.");
        }
    }
}
