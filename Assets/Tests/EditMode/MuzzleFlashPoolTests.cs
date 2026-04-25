using NUnit.Framework;
using UnityEngine;
using JerryScripts.Feature.WeaponHandling;

namespace JerryScripts.Tests.EditMode
{
    /// <summary>
    /// NUnit EditMode unit tests for <see cref="MuzzleFlashPool"/>. S1-007.
    ///
    /// All tests construct a minimal scene-less environment by creating a GameObject and
    /// calling <see cref="MuzzleFlashPool.InjectPrefabAndSize"/> to bypass Awake pool
    /// construction (Awake requires <c>Instantiate</c>, which needs a prefab asset — not
    /// available headlessly). Each test tears down its own GameObjects via
    /// <c>Object.DestroyImmediate</c> to avoid cross-test contamination.
    ///
    /// Coverage:
    ///   1. Round-robin cursor advances correctly after Spawn().
    ///   2. Spawn wraps cursor at pool boundary (index modulo pool size).
    ///   3. Spawn activates the correct pool slot's GameObject.
    ///   4. Spawn sets world-space position and rotation on the spawned instance.
    /// </summary>
    [TestFixture]
    public sealed class MuzzleFlashPoolTests
    {
        // ===================================================================
        // Helpers
        // ===================================================================

        /// <summary>
        /// Creates a minimal <see cref="MuzzleFlashPool"/> component on a fresh GameObject.
        /// Caller owns teardown via <c>Object.DestroyImmediate(go)</c>.
        /// </summary>
        private static (GameObject go, MuzzleFlashPool pool) CreatePool()
        {
            var go   = new GameObject("MuzzleFlashPool_Test");
            var pool = go.AddComponent<MuzzleFlashPool>();
            return (go, pool);
        }

        /// <summary>
        /// Creates an array of <paramref name="count"/> inactive GameObjects each with a
        /// <see cref="ParticleSystem"/> component, simulating pre-warmed pool instances.
        /// Caller owns teardown.
        /// </summary>
        private static ParticleSystem[] CreateFakeInstances(int count)
        {
            var instances = new ParticleSystem[count];
            for (int i = 0; i < count; i++)
            {
                var go  = new GameObject($"FakeFlash_{i:D2}");
                var ps  = go.AddComponent<ParticleSystem>();
                go.SetActive(false);   // pool pre-state: inactive
                instances[i] = ps;
            }
            return instances;
        }

        /// <summary>Destroys all GameObjects in an array of <see cref="ParticleSystem"/> instances.</summary>
        private static void DestroyInstances(ParticleSystem[] instances)
        {
            foreach (var ps in instances)
                if (ps != null) Object.DestroyImmediate(ps.gameObject);
        }

        // ===================================================================
        // Test 1 — cursor advances after Spawn
        // ===================================================================

        /// <summary>
        /// After one <see cref="MuzzleFlashPool.Spawn"/> call, <c>ActiveCursor</c> must
        /// advance from 0 to 1. Verifies the round-robin step is exactly +1.
        /// </summary>
        [Test]
        public void MuzzleFlashPool_Spawn_AdvancesCursorByOne()
        {
            // Arrange
            var (go, pool)     = CreatePool();
            ParticleSystem[] instances = CreateFakeInstances(3);
            pool.InjectPrefabAndSize(instances, cursorStart: 0);

            // Act
            pool.Spawn(Vector3.zero, Quaternion.identity);

            // Assert
            Assert.AreEqual(1, pool.ActiveCursor,
                "ActiveCursor must advance by 1 after a single Spawn call.");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 2 — cursor wraps at pool boundary
        // ===================================================================

        /// <summary>
        /// When <c>ActiveCursor</c> is at the last valid index (poolSize - 1),
        /// the next <see cref="MuzzleFlashPool.Spawn"/> must wrap it back to 0.
        /// Verifies modulo wrap-around (no IndexOutOfRangeException on boundary).
        /// </summary>
        [Test]
        public void MuzzleFlashPool_Spawn_WrapsCursorAtPoolBoundary()
        {
            // Arrange
            const int poolSize = 3;
            var (go, pool)     = CreatePool();
            ParticleSystem[] instances = CreateFakeInstances(poolSize);
            pool.InjectPrefabAndSize(instances, cursorStart: poolSize - 1);

            // Act — spawn from the last slot; cursor must wrap to 0
            pool.Spawn(Vector3.zero, Quaternion.identity);

            // Assert
            Assert.AreEqual(0, pool.ActiveCursor,
                $"ActiveCursor must wrap to 0 after Spawn from last index (poolSize={poolSize}).");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 3 — Spawn activates the correct pool slot
        // ===================================================================

        /// <summary>
        /// <see cref="MuzzleFlashPool.Spawn"/> must activate the GameObject at the
        /// pre-Spawn cursor index. With cursor at 0, slot 0's GO becomes active;
        /// slots 1 and 2 remain inactive.
        /// </summary>
        [Test]
        public void MuzzleFlashPool_Spawn_ActivatesCorrectSlotGameObject()
        {
            // Arrange
            var (go, pool)     = CreatePool();
            ParticleSystem[] instances = CreateFakeInstances(3);
            pool.InjectPrefabAndSize(instances, cursorStart: 0);

            // Pre-condition: all inactive
            for (int i = 0; i < instances.Length; i++)
                Assert.IsFalse(instances[i].gameObject.activeSelf,
                    $"Pre-condition failed: slot {i} should be inactive before Spawn.");

            // Act
            pool.Spawn(Vector3.zero, Quaternion.identity);

            // Assert — slot 0 activated, others still inactive
            Assert.IsTrue(instances[0].gameObject.activeSelf,
                "Slot 0 (cursor pre-Spawn position) must be activated by Spawn.");
            Assert.IsFalse(instances[1].gameObject.activeSelf,
                "Slot 1 must remain inactive — not selected by this Spawn.");
            Assert.IsFalse(instances[2].gameObject.activeSelf,
                "Slot 2 must remain inactive — not selected by this Spawn.");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 4 — Spawn sets correct world-space position and rotation
        // ===================================================================

        /// <summary>
        /// <see cref="MuzzleFlashPool.Spawn"/> must place the spawned instance at
        /// exactly the supplied world-space <paramref name="position"/> and <paramref name="rotation"/>.
        /// Verifies that muzzle-flash VFX appear at the muzzle tip, not at origin.
        /// </summary>
        [Test]
        public void MuzzleFlashPool_Spawn_SetsWorldSpacePositionAndRotation()
        {
            // Arrange
            var (go, pool)     = CreatePool();
            ParticleSystem[] instances = CreateFakeInstances(2);
            pool.InjectPrefabAndSize(instances, cursorStart: 0);

            Vector3    expectedPos = new Vector3(1.5f, 0.8f, 3.2f);
            Quaternion expectedRot = Quaternion.Euler(0f, 45f, 0f);

            // Act
            pool.Spawn(expectedPos, expectedRot);

            // Assert
            Transform spawnedTransform = instances[0].transform;

            Assert.AreEqual(
                expectedPos,
                spawnedTransform.position,
                "Spawned instance world position must match the supplied muzzle position.");

            Assert.AreEqual(
                expectedRot,
                spawnedTransform.rotation,
                "Spawned instance world rotation must match the supplied muzzle rotation.");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }
    }
}
