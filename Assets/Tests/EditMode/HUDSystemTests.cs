using NUnit.Framework;
using UnityEngine;
using JerryScripts.Presentation.HUD;

namespace JerryScripts.Tests.EditMode
{
    /// <summary>
    /// HUDSystem tests are STALE on the <c>final-integration</c> branch.
    ///
    /// <para>The Phase 4 refactor (sprint-final.md) removed the dependencies these tests
    /// were built around: <c>IRigControllerProvider</c> (replaced by Inspector Transform
    /// override), <c>WeaponInstance</c>/<c>WeaponData</c>/<c>WeaponInstanceState</c>
    /// (replaced by <c>BNGWeaponBridge</c> + Sam's <c>GeneratedWeaponManager</c>), and the
    /// <c>InjectDependencies</c> test seam (deleted).</para>
    ///
    /// <para><b>TODO</b>: rewrite tests against the new API:
    /// <list type="bullet">
    ///   <item><see cref="HUDConfig"/> default values (still useful — was passing before).</item>
    ///   <item><see cref="HUDSystem"/>.<c>TestSimulateEquipChanged(bool, GeneratedWeaponManager)</c>
    ///         + the cached <c>_statFill*</c> seam reads — verify the new normalization formulas
    ///         (sprint-final.md §Stat Mapping).</item>
    ///   <item>Bridge-based equip-state replay verification.</item>
    /// </list></para>
    ///
    /// <para>Until the rewrite, only the (passing) HUDConfig defaults test is kept.</para>
    /// </summary>
    [TestFixture]
    public sealed class HUDSystemTests
    {
        [Test]
        public void test_HUDConfig_defaults_produce_valid_dimensions()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<HUDConfig>();

            // Assert — these defaults survived the Phase 4 refactor unchanged
            Assert.That(config.PanelWidth, Is.EqualTo(0.12f).Within(0.001f));
            Assert.That(config.PanelHeight, Is.EqualTo(0.04f).Within(0.001f));
            Assert.That(config.BgOpacity, Is.EqualTo(0.7f).Within(0.01f));
            Assert.That(config.LocalOffset, Is.EqualTo(new Vector3(0f, 0.08f, -0.05f)));
            Assert.That(config.LocalRotation, Is.EqualTo(new Vector3(-30f, 0f, 0f)));

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void test_HUDConfig_text_color_is_warm_off_white()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<HUDConfig>();

            // Assert — #E8E8D0 = (0.91, 0.91, 0.82, 1.0)
            Assert.That(config.TextColor.r, Is.EqualTo(0.91f).Within(0.01f));
            Assert.That(config.TextColor.g, Is.EqualTo(0.91f).Within(0.01f));
            Assert.That(config.TextColor.b, Is.EqualTo(0.82f).Within(0.01f));
            Assert.That(config.TextColor.a, Is.EqualTo(1f));

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void test_HUDConfig_health_bar_colors_are_distinct()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<HUDConfig>();

            // Assert
            Assert.That(config.HealthBarFillColor, Is.Not.EqualTo(config.HealthBarBgColor));

            UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
