using NUnit.Framework;
using UnityEngine;
using JerryScripts.Feature.WeaponHandling;

namespace JerryScripts.Tests.EditMode
{
    /// <summary>
    /// NUnit EditMode unit tests for <see cref="MagSpawnPool"/>. S2-001.
    ///
    /// All tests construct a minimal scene-less environment by creating a GameObject
    /// and calling <see cref="MagSpawnPool.InjectPool"/> /
    /// <see cref="MagSpawnPool.InjectActiveSpawnedMag"/> to bypass Awake pool
    /// construction and coroutine-driven spawn (coroutines require a running
    /// UnityEngine loop — not available in EditMode). Each test tears down its
    /// own GameObjects via <c>Object.DestroyImmediate</c> to avoid cross-test
    /// contamination.
    ///
    /// Coverage:
    ///   1. <c>ActiveSpawnedMag</c> is null before any spawn (initial pool state).
    ///   2. <c>ReturnActive</c> deactivates and unparents the active magazine and
    ///      clears <c>ActiveSpawnedMag</c>.
    ///   3. <c>ReturnActive</c> is a no-op when <c>ActiveSpawnedMag</c> is null
    ///      (idempotency guard).
    ///   4. <c>ActiveCursor</c> starts at 0 after injection with cursorStart=0.
    ///   5. <see cref="MagSpawnPool"/> implements the <c>ActiveSpawnedMag</c>
    ///      public property contract (reflected — compile-time shape guard).
    /// </summary>
    [TestFixture]
    public sealed class MagSpawnPoolTests
    {
        // ===================================================================
        // Helpers
        // ===================================================================

        /// <summary>
        /// Creates a minimal <see cref="MagSpawnPool"/> component on a fresh GameObject.
        /// Caller owns teardown via <c>Object.DestroyImmediate(go)</c>.
        /// </summary>
        private static (GameObject go, MagSpawnPool pool) CreatePool()
        {
            var go   = new GameObject("MagSpawnPool_Test");
            var pool = go.AddComponent<MagSpawnPool>();
            return (go, pool);
        }

        /// <summary>
        /// Creates an array of <paramref name="count"/> inactive GameObjects simulating
        /// pre-warmed pool instances. Caller owns teardown.
        /// </summary>
        private static GameObject[] CreateFakeInstances(int count)
        {
            var instances = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"FakeMagSpawn_{i:D2}");
                go.SetActive(false);
                instances[i] = go;
            }
            return instances;
        }

        private static void DestroyInstances(GameObject[] instances)
        {
            foreach (var go in instances)
                if (go != null) Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 1 — ActiveSpawnedMag is null on fresh pool
        // ===================================================================

        /// <summary>
        /// <see cref="MagSpawnPool.ActiveSpawnedMag"/> must be null immediately after
        /// the pool is injected — no magazine has been spawned yet.
        /// </summary>
        [Test]
        public void MagSpawnPool_ActiveSpawnedMag_IsNullBeforeSpawn()
        {
            // Arrange
            var (go, pool) = CreatePool();
            var instances  = CreateFakeInstances(2);
            pool.InjectPool(instances, cursorStart: 0);

            // Assert
            Assert.IsNull(pool.ActiveSpawnedMag,
                "ActiveSpawnedMag must be null before any Spawn call.");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 2 — ReturnActive deactivates, unparents, clears property
        // ===================================================================

        /// <summary>
        /// <see cref="MagSpawnPool.ReturnActive"/> must deactivate the active
        /// magazine GameObject, unparent it back to the pool root, and set
        /// <see cref="MagSpawnPool.ActiveSpawnedMag"/> to null.
        /// </summary>
        [Test]
        public void MagSpawnPool_ReturnActive_DeactivatesAndClearsActiveMag()
        {
            // Arrange
            var (go, pool) = CreatePool();
            var instances  = CreateFakeInstances(2);
            pool.InjectPool(instances, cursorStart: 0);

            // Simulate a post-spawn state: activate instance 0, parent to a fake
            // off-hand transform, and inject it as the active magazine.
            var offHand = new GameObject("FakeOffHand");
            instances[0].transform.SetParent(offHand.transform, worldPositionStays: false);
            instances[0].SetActive(true);
            pool.InjectActiveSpawnedMag(instances[0]);

            Assert.IsNotNull(pool.ActiveSpawnedMag,
                "Pre-condition: ActiveSpawnedMag must be non-null after InjectActiveSpawnedMag.");
            Assert.IsTrue(instances[0].activeSelf,
                "Pre-condition: instance 0 must be active.");

            // Act
            pool.ReturnActive();

            // Assert — cleared
            Assert.IsNull(pool.ActiveSpawnedMag,
                "ActiveSpawnedMag must be null after ReturnActive.");

            // Assert — deactivated
            Assert.IsFalse(instances[0].activeSelf,
                "Returned magazine instance must be inactive after ReturnActive.");

            // Assert — unparented (now a child of pool GO, not offHand)
            Assert.AreNotEqual(offHand.transform, instances[0].transform.parent,
                "Returned magazine must not remain parented to the off-hand transform.");

            // Teardown
            Object.DestroyImmediate(offHand);
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 3 — ReturnActive is idempotent when null
        // ===================================================================

        /// <summary>
        /// Calling <see cref="MagSpawnPool.ReturnActive"/> when
        /// <see cref="MagSpawnPool.ActiveSpawnedMag"/> is null must be a no-op —
        /// no exception, no state mutation, no warning.
        /// </summary>
        [Test]
        public void MagSpawnPool_ReturnActive_IsNoOpWhenActiveSpawnedMagIsNull()
        {
            // Arrange
            var (go, pool) = CreatePool();
            var instances  = CreateFakeInstances(2);
            pool.InjectPool(instances, cursorStart: 0);

            Assert.IsNull(pool.ActiveSpawnedMag,
                "Pre-condition: ActiveSpawnedMag must be null.");

            // Act — must not throw
            Assert.DoesNotThrow(() => pool.ReturnActive(),
                "ReturnActive must not throw when ActiveSpawnedMag is null.");

            // Assert — still null
            Assert.IsNull(pool.ActiveSpawnedMag,
                "ActiveSpawnedMag must remain null after a no-op ReturnActive.");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 4 — ActiveCursor initialises correctly via InjectPool
        // ===================================================================

        /// <summary>
        /// <c>ActiveCursor</c> must equal the <c>cursorStart</c> argument supplied
        /// to <see cref="MagSpawnPool.InjectPool"/>. With cursorStart=0, cursor must
        /// be 0. Verifies the test-seam correctly sets initial state.
        /// </summary>
        [Test]
        public void MagSpawnPool_InjectPool_SetsCursorToSuppliedStart()
        {
            // Arrange
            var (go, pool) = CreatePool();
            var instances  = CreateFakeInstances(2);

            // Act
            pool.InjectPool(instances, cursorStart: 1);

            // Assert
            Assert.AreEqual(1, pool.ActiveCursor,
                "ActiveCursor must match the cursorStart argument passed to InjectPool.");

            // Teardown
            DestroyInstances(instances);
            Object.DestroyImmediate(go);
        }

        // ===================================================================
        // Test 5 — ActiveSpawnedMag property is publicly readable (shape guard)
        // ===================================================================

        /// <summary>
        /// <see cref="MagSpawnPool.ActiveSpawnedMag"/> must be a public, readable
        /// property of type <c>GameObject</c>. <c>MagWellSocket</c> reads this
        /// property every Update — removing or renaming it breaks the proximity
        /// check without a compile error in the socket. This reflection test
        /// surfaces the regression in CI before runtime.
        /// </summary>
        [Test]
        public void MagSpawnPool_ActiveSpawnedMag_IsPublicReadableProperty()
        {
            // Arrange
            var propInfo = typeof(MagSpawnPool)
                .GetProperty(
                    "ActiveSpawnedMag",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            // Assert — property exists
            Assert.IsNotNull(propInfo,
                "MagSpawnPool must expose a public property named 'ActiveSpawnedMag'. " +
                "MagWellSocket reads it every Update frame.");

            // Assert — correct return type
            Assert.AreEqual(typeof(GameObject), propInfo.PropertyType,
                "ActiveSpawnedMag must be of type GameObject.");

            // Assert — has a getter
            Assert.IsTrue(propInfo.CanRead,
                "ActiveSpawnedMag must have a public getter.");
        }
    }
}
