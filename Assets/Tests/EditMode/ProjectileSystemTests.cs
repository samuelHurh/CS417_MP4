using NUnit.Framework;
using JerryScripts.Core.Projectile;

namespace JerryScripts.Tests.EditMode
{
    /// <summary>
    /// NUnit EditMode unit tests for the S1-005 Projectile System (hitscan scope).
    ///
    /// Scope: Jerry's player-side hitscan only. Enemy projectile pooling is Sam's scope
    /// and has been removed from this file accordingly.
    ///
    /// What's NOT tested here (by design):
    /// - <c>Physics.Raycast</c> behaviour — requires a live physics scene (PlayMode only)
    /// - <c>FireHitscan</c> hit/miss resolution — requires a scene with colliders
    /// These are covered by PlayMode integration tests deferred to a later story.
    /// </summary>
    [TestFixture]
    public sealed class ProjectileSystemTests
    {
        // ===================================================================
        // Interface shape — compile-time contract guard
        // ===================================================================

        /// <summary>
        /// Ensures <see cref="IProjectileService"/> still exposes only the hitscan method.
        /// If a future refactor re-introduces enemy projectile methods to the interface,
        /// this test breaks at compile time, forcing the author to confirm the scope
        /// change with the team.
        /// </summary>
        [Test]
        public void IProjectileService_ExposesFireHitscanOnly()
        {
            // Arrange
            System.Reflection.MethodInfo[] methods = typeof(IProjectileService)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            // Assert
            Assert.AreEqual(1, methods.Length,
                "IProjectileService must expose exactly one method (FireHitscan). " +
                "Enemy projectile spawning is Sam's Enemy System scope and must not return here.");

            Assert.AreEqual("FireHitscan", methods[0].Name,
                "The only method on IProjectileService must be FireHitscan.");
        }

        /// <summary>
        /// <see cref="ProjectileSystem"/> must implement <see cref="IProjectileService"/>.
        /// Compile-time check expressed as a test so regressions are caught in CI.
        /// </summary>
        [Test]
        public void ProjectileSystem_ImplementsIProjectileService()
        {
            Assert.IsTrue(
                typeof(IProjectileService).IsAssignableFrom(typeof(ProjectileSystem)),
                "ProjectileSystem must implement IProjectileService.");
        }
    }
}
