using NUnit.Framework;
using UnityEngine;
using JerryScripts.Feature.WeaponHandling;

namespace JerryScripts.Tests.EditMode
{
    /// <summary>
    /// NUnit EditMode unit tests for <see cref="MagDropPool"/>. S1-009.
    ///
    /// All tests construct a minimal scene-less environment by creating a GameObject and
    /// calling <see cref="MagDropPool.InjectPool"/> to bypass Awake pool construction
    /// (Awake requires <c>Instantiate</c> + a prefab asset — not available headlessly).
    /// Each test tears down its own GameObjects via <c>Object.DestroyImmediate</c> to
    /// avoid cross-test contamination.
    ///
    /// Coverage:
    ///   1. Round-robin cursor advances correctly after Eject().
    ///   2. Eject wraps cursor at pool boundary (index modulo pool size).
    ///   3. Eject activates the correct pool slot's GameObject.
    ///   4. Eject sets world-space position and rotation on the ejected instance.
    ///   5. Eject sets the Rigidbody to non-kinematic so the magazine falls with physics.
    /// </summary>
    [TestFixture]
    public sealed class MagDropPoolTests
    {
        // ===================================================================
        // Helpers
        // ===================================================================

        /// <summary>
        /// Creates a minimal <see cref="MagDropPool"/> component on a fresh GameObject.
        /// Caller owns teardown via <c>Object.DestroyImmediate(go)</c>.
        /// </summary>
        private static (GameObject go, MagDropPool pool) CreatePool()
        {
            var go   = new GameObject("MagDropPool_Test");
            var pool = go.AddComponent<MagDropPool>();
            return (go, pool);
        }

        /// <summary>
        /// Creates an array of <paramref name="count"/> inactive GameObjects each with a
        /// <see cref="Rigidbody"/> component, simulating pre-warmed pool instances.
        /// Caller owns teardown.
        /// </summary>
        private static (GameObject[] instances, Rigidbody[] rigidbodies) CreateFakeInstances(int count)
        {
            var instances   = new GameObject[count];
            var rigidbodies = new Rigidbody[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"FakeMag_{i:D2}");
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;   // pool pre-state: kinematic + inactive
                go.SetActive(false);
                instances[i]   = go;
                rigidbodies[i] = rb;
            }

            return (instances, rigidbodies);
        }

        /// <summary>Destroys all GameObjects in an array of instances.</summary>
        private static void DestroyInstances(GameObject[] instances)
        {
            foreach (var go in instances)
                if (go != null) Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 1 — cursor advances after Eject
        // ===================================================================

        /// <summary>
        /// After one <see cref="MagDropPool.Eject"/> call, <c>ActiveCursor</c> must
        /// advance from 0 to 1. Verifies the round-robin step is exactly +1.
        /// </summary>
        [Test]
        public void MagDropPool_Eject_AdvancesCursorByOne()
        {
            // Arrange
            var (go, pool) = CreatePool();
            var (instances, rigidbodies) = CreateFakeInstances(3);
            pool.InjectPool(instances, rigidbodies, cursorStart: 0);

            // Act
            pool.Eject(Vector3.zero, Quaternion.identity, persistSeconds: 8f);

            // Assert
            Assert.AreEqual(1, pool.ActiveCursor,
                "ActiveCursor must advance by 1 after a single Eject call.");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 2 — cursor wraps at pool boundary
        // ===================================================================

        /// <summary>
        /// When <c>ActiveCursor</c> is at the last valid index (poolSize - 1),
        /// the next <see cref="MagDropPool.Eject"/> must wrap it back to 0.
        /// Verifies modulo wrap-around (no IndexOutOfRangeException on boundary).
        /// </summary>
        [Test]
        public void MagDropPool_Eject_WrapsCursorAtPoolBoundary()
        {
            // Arrange
            const int poolSize = 3;
            var (go, pool) = CreatePool();
            var (instances, rigidbodies) = CreateFakeInstances(poolSize);
            pool.InjectPool(instances, rigidbodies, cursorStart: poolSize - 1);

            // Act — eject from the last slot; cursor must wrap to 0
            pool.Eject(Vector3.zero, Quaternion.identity, persistSeconds: 8f);

            // Assert
            Assert.AreEqual(0, pool.ActiveCursor,
                $"ActiveCursor must wrap to 0 after Eject from last index (poolSize={poolSize}).");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 3 — Eject activates the correct pool slot
        // ===================================================================

        /// <summary>
        /// <see cref="MagDropPool.Eject"/> must activate the GameObject at the
        /// pre-Eject cursor index. With cursor at 0, slot 0's GO becomes active;
        /// slots 1 and 2 remain inactive.
        /// </summary>
        [Test]
        public void MagDropPool_Eject_ActivatesCorrectSlotGameObject()
        {
            // Arrange
            var (go, pool) = CreatePool();
            var (instances, rigidbodies) = CreateFakeInstances(3);
            pool.InjectPool(instances, rigidbodies, cursorStart: 0);

            // Pre-condition: all inactive
            for (int i = 0; i < instances.Length; i++)
                Assert.IsFalse(instances[i].activeSelf,
                    $"Pre-condition failed: slot {i} should be inactive before Eject.");

            // Act
            pool.Eject(Vector3.zero, Quaternion.identity, persistSeconds: 8f);

            // Assert — slot 0 activated, others still inactive
            Assert.IsTrue(instances[0].activeSelf,
                "Slot 0 (cursor pre-Eject position) must be activated by Eject.");
            Assert.IsFalse(instances[1].activeSelf,
                "Slot 1 must remain inactive — not selected by this Eject.");
            Assert.IsFalse(instances[2].activeSelf,
                "Slot 2 must remain inactive — not selected by this Eject.");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 4 — Eject sets correct world-space position and rotation
        // ===================================================================

        /// <summary>
        /// <see cref="MagDropPool.Eject"/> must place the ejected instance at exactly the
        /// supplied world-space position and rotation.
        /// Verifies that the magazine appears at the mag-well socket, not at origin.
        /// </summary>
        [Test]
        public void MagDropPool_Eject_SetsWorldSpacePositionAndRotation()
        {
            // Arrange
            var (go, pool) = CreatePool();
            var (instances, rigidbodies) = CreateFakeInstances(2);
            pool.InjectPool(instances, rigidbodies, cursorStart: 0);

            Vector3    expectedPos = new Vector3(0.3f, 1.1f, -0.2f);
            Quaternion expectedRot = Quaternion.Euler(0f, 90f, 0f);

            // Act
            pool.Eject(expectedPos, expectedRot, persistSeconds: 8f);

            // Assert
            Transform ejectedTransform = instances[0].transform;

            Assert.AreEqual(
                expectedPos,
                ejectedTransform.position,
                "Ejected instance world position must match the supplied mag-well position.");

            // Quaternion equality must use angle tolerance — NUnit's default Equals
            // compares all 4 components for exact float match, which fails on values
            // produced by Quaternion.Euler even when ToString output matches.
            float angleDelta = Quaternion.Angle(expectedRot, ejectedTransform.rotation);
            Assert.That(angleDelta, Is.LessThan(0.01f),
                $"Ejected instance world rotation must match the supplied mag-well rotation " +
                $"(angle delta = {angleDelta}°).");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 5 — Eject un-kinematics the Rigidbody (physics drop)
        // ===================================================================

        /// <summary>
        /// <see cref="MagDropPool.Eject"/> must set <c>Rigidbody.isKinematic = false</c>
        /// on the ejected slot so it falls under gravity (GDD Rule 11: mag drops with physics).
        /// </summary>
        [Test]
        public void MagDropPool_Eject_SetsRigidbodyNonKinematic()
        {
            // Arrange
            var (go, pool) = CreatePool();
            var (instances, rigidbodies) = CreateFakeInstances(2);
            // Confirm pre-condition: rigidbody starts kinematic (pool idle state)
            Assert.IsTrue(rigidbodies[0].isKinematic,
                "Pre-condition: Rigidbody must start kinematic before Eject.");
            pool.InjectPool(instances, rigidbodies, cursorStart: 0);

            // Act
            pool.Eject(Vector3.zero, Quaternion.identity, persistSeconds: 8f);

            // Assert
            Assert.IsFalse(rigidbodies[0].isKinematic,
                "Rigidbody.isKinematic must be false after Eject so the magazine falls with physics.");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }
    }
}
