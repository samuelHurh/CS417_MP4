using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using JerryScripts.Foundation;
using JerryScripts.Feature.WeaponHandling;

namespace JerryScripts.Tests.EditMode
{
    /// <summary>
    /// NUnit EditMode unit tests for <see cref="WeaponInstance"/> reload path. S2-001.
    ///
    /// <para>Scope: the reload FSM contract exposed through the
    /// <see cref="IMagInsertReceiver"/> interface — specifically the
    /// <see cref="WeaponInstance.CompleteReload"/> method and the
    /// <c>IMagInsertReceiver</c> implementation guard. We test the
    /// state-machine transitions and guard conditions without needing a
    /// full XRI scene; all Unity-engine dependencies on the weapon are
    /// bypassed by injecting state directly via reflection on private fields.</para>
    ///
    /// <para><b>Why reflection injection?</b>
    /// <c>WeaponInstance</c> requires <c>XRGrabInteractable</c> and
    /// <c>Rigidbody</c> on its GameObject (enforced by <c>[RequireComponent]</c>),
    /// and its <c>Awake</c> performs scene-wide <c>FindAnyObjectByType</c> calls
    /// and references those components. EditMode tests cannot run a live physics
    /// simulation. We therefore skip <c>Awake</c> by never calling it, add the
    /// required components manually, and inject the private FSM state field
    /// (<c>CurrentState</c> is a property with a private setter — set via
    /// <c>PropertyInfo.SetValue</c>) plus the <c>_data</c> field to exercise the
    /// reload path in isolation.</para>
    ///
    /// Coverage:
    ///   1. <see cref="WeaponInstance"/> implements <see cref="IMagInsertReceiver"/>
    ///      (interface shape guard — catches regressions without instantiating the full weapon).
    ///   2. <c>CompleteReload</c> from <c>Reloading</c> + tactical (was not empty)
    ///      → FSM advances to <c>Held</c>.
    ///   3. <c>CompleteReload</c> from <c>Reloading</c> + dry (was empty)
    ///      → FSM advances to <c>SlideBack</c>.
    ///   4. <c>CompleteReload</c> called outside <c>Reloading</c> state is a
    ///      warn-and-ignore (state unchanged, no exception).
    ///   5. <c>CompleteReload</c> resets <c>CurrentAmmo</c> to <c>WeaponData.MagCapacity</c>.
    /// </summary>
    [TestFixture]
    public sealed class WeaponInstanceReloadTests
    {
        // ===================================================================
        // Reflection helpers
        // ===================================================================

        private static readonly PropertyInfo s_currentStateProp =
            typeof(WeaponInstance).GetProperty(
                "CurrentState",
                BindingFlags.Public | BindingFlags.Instance);

        private static readonly FieldInfo s_wasEmptyField =
            typeof(WeaponInstance).GetField(
                "_wasEmptyBeforeReload",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo s_dataField =
            typeof(WeaponInstance).GetField(
                "_data",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo s_rigidbodyField =
            typeof(WeaponInstance).GetField(
                "_rigidbody",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo s_grabInteractableField =
            typeof(WeaponInstance).GetField(
                "_grabInteractable",
                BindingFlags.NonPublic | BindingFlags.Instance);

        // ===================================================================
        // Fixture helpers
        // ===================================================================

        /// <summary>
        /// Creates a <see cref="WeaponInstance"/> with the minimum required components
        /// attached and private dependencies injected, without triggering <c>Awake</c>.
        ///
        /// <para>We add <c>XRGrabInteractable</c> (which pulls in a <c>Rigidbody</c>
        /// automatically) and then inject the cached component references that
        /// <c>Awake</c> would normally populate, allowing <c>EnterState</c> (called
        /// by <c>CompleteReload</c>) to run without a NullReferenceException.</para>
        /// </summary>
        private static (GameObject go, WeaponInstance weapon, WeaponData data) CreateWeapon()
        {
            var go = new GameObject("WeaponInstance_Test");

            // [RequireComponent] demands both of these — add them before AddComponent<WeaponInstance>
            var rb = go.AddComponent<Rigidbody>();
            var xrig = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            var weapon = go.AddComponent<WeaponInstance>();

            // Inject the private cached references that Awake sets — without these,
            // EnterState() throws a NullReferenceException on the Rigidbody.
            s_rigidbodyField.SetValue(weapon, rb);
            s_grabInteractableField.SetValue(weapon, xrig);

            // Create and inject a WeaponData SO with default values.
            var data = ScriptableObject.CreateInstance<WeaponData>();
            s_dataField.SetValue(weapon, data);

            return (go, weapon, data);
        }

        /// <summary>
        /// Sets <c>CurrentState</c> on the weapon instance via the property's
        /// private setter (simulates being in a given FSM state).
        /// </summary>
        private static void ForceState(WeaponInstance weapon, WeaponInstanceState state)
        {
            s_currentStateProp.SetValue(weapon, state);
        }

        /// <summary>
        /// Sets <c>_wasEmptyBeforeReload</c> on the weapon instance via reflection.
        /// </summary>
        private static void ForceWasEmpty(WeaponInstance weapon, bool wasEmpty)
        {
            s_wasEmptyField.SetValue(weapon, wasEmpty);
        }

        // ===================================================================
        // Test 1 — IMagInsertReceiver interface shape guard
        // ===================================================================

        /// <summary>
        /// <see cref="WeaponInstance"/> must implement <see cref="IMagInsertReceiver"/>.
        /// This is a compile-time contract expressed as a test — if the interface
        /// is accidentally removed from the class declaration, CI catches it
        /// immediately without needing to instantiate the full weapon prefab.
        /// </summary>
        [Test]
        public void WeaponInstance_ImplementsIMagInsertReceiver()
        {
            Assert.IsTrue(
                typeof(IMagInsertReceiver).IsAssignableFrom(typeof(WeaponInstance)),
                "WeaponInstance must implement IMagInsertReceiver. " +
                "MagWellSocket calls CompleteReload through this interface.");
        }

        // ===================================================================
        // Test 2 — tactical reload (not empty) → Held
        // ===================================================================

        /// <summary>
        /// GDD Rule 12: when the weapon was NOT empty before reload (tactical reload),
        /// <c>CompleteReload</c> must advance the FSM to <c>Held</c> — no slide-back.
        /// </summary>
        [Test]
        public void WeaponInstance_CompleteReload_TacticalReload_TransitionsToHeld()
        {
            // Arrange
            var (go, weapon, data) = CreateWeapon();
            ForceState(weapon, WeaponInstanceState.Reloading);
            ForceWasEmpty(weapon, false);   // tactical reload — magazine was not empty

            // Act
            weapon.CompleteReload();

            // Assert
            Assert.AreEqual(WeaponInstanceState.Held, weapon.CurrentState,
                "Tactical reload (was not empty) must transition FSM to Held, not SlideBack.");

            // Teardown
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
        }

        // ===================================================================
        // Test 3 — dry reload (was empty) → SlideBack
        // ===================================================================

        /// <summary>
        /// GDD Rule 12: when the weapon WAS empty before reload (dry reload),
        /// <c>CompleteReload</c> must advance the FSM to <c>SlideBack</c>.
        /// The player must rack the slide before firing again.
        /// </summary>
        [Test]
        public void WeaponInstance_CompleteReload_DryReload_TransitionsToSlideBack()
        {
            // Arrange
            var (go, weapon, data) = CreateWeapon();
            ForceState(weapon, WeaponInstanceState.Reloading);
            ForceWasEmpty(weapon, true);    // dry reload — magazine was empty

            // Act
            weapon.CompleteReload();

            // Assert
            Assert.AreEqual(WeaponInstanceState.SlideBack, weapon.CurrentState,
                "Dry reload (was empty) must transition FSM to SlideBack (GDD Rule 12).");

            // Teardown
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
        }

        // ===================================================================
        // Test 4 — CompleteReload outside Reloading state is a no-op
        // ===================================================================

        /// <summary>
        /// Calling <c>CompleteReload</c> while the weapon is in any state other
        /// than <c>Reloading</c> must be a warn-and-ignore: state is unchanged,
        /// no exception is thrown.
        /// </summary>
        [Test]
        public void WeaponInstance_CompleteReload_OutsideReloadingState_DoesNotChangeState()
        {
            // Arrange
            var (go, weapon, data) = CreateWeapon();
            ForceState(weapon, WeaponInstanceState.Held);   // not Reloading

            // Act — must not throw; must not change state
            Assert.DoesNotThrow(() => weapon.CompleteReload(),
                "CompleteReload must not throw when called outside the Reloading state.");

            // Assert — state unchanged
            Assert.AreEqual(WeaponInstanceState.Held, weapon.CurrentState,
                "FSM state must remain Held when CompleteReload is called outside Reloading.");

            // Teardown
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
        }

        // ===================================================================
        // Test 5 — CompleteReload resets CurrentAmmo to MagCapacity
        // ===================================================================

        /// <summary>
        /// <c>CompleteReload</c> must set <c>CurrentAmmo</c> to
        /// <c>WeaponData.MagCapacity</c> (GDD Rule 10: fresh magazine = full mag).
        /// Verified for the tactical-reload path (was not empty) — the ammo reset
        /// is identical for both tactical and dry reload paths.
        /// </summary>
        [Test]
        public void WeaponInstance_CompleteReload_ResetsCurrentAmmoToMagCapacity()
        {
            // Arrange
            var (go, weapon, data) = CreateWeapon();
            ForceState(weapon, WeaponInstanceState.Reloading);
            ForceWasEmpty(weapon, false);

            // Force ammo to a depleted value before CompleteReload
            typeof(WeaponInstance)
                .GetProperty("CurrentAmmo", BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(weapon, 3);

            // Act
            weapon.CompleteReload();

            // Assert — ammo must equal MagCapacity (default SO value = 12)
            Assert.AreEqual(data.MagCapacity, weapon.CurrentAmmo,
                $"CurrentAmmo must equal WeaponData.MagCapacity ({data.MagCapacity}) after CompleteReload.");

            // Teardown
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
        }
    }
}
