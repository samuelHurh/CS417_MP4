namespace JerryScripts.Foundation
{
    /// <summary>
    /// Implemented by any object that can accept a magazine insertion event.
    ///
    /// <para><b>Primary implementor:</b> <c>WeaponInstance</c> — called by
    /// <c>MagWellSocket</c> when the player's off-hand magazine enters the
    /// mag-well proximity radius and the weapon is in the
    /// <c>WeaponInstanceState.Reloading</c> state.</para>
    ///
    /// <para><b>Dependency direction:</b> Foundation-layer — zero upstream
    /// dependencies. Feature and Core layers implement or consume this interface;
    /// this must never depend on them.</para>
    ///
    /// <para><b>Design rationale:</b> Inverting the dependency with an interface
    /// keeps <c>MagWellSocket</c> decoupled from <c>WeaponInstance</c>'s concrete
    /// type. A socket can complete a reload without knowing any weapon internals,
    /// and tests can supply a trivial spy without instantiating the full weapon
    /// prefab hierarchy.</para>
    /// </summary>
    /// <remarks>
    /// S2-001: Magazine spawn + insertion proximity detection.
    /// GDD: core-fps-weapon-handling.md §Reload Mechanic, Rules 10–12.
    /// </remarks>
    public interface IMagInsertReceiver
    {
        /// <summary>
        /// Called when the magazine reaches the mag-well and the insertion is complete.
        /// The receiver is responsible for resetting ammo, posting audio, and
        /// advancing its FSM (Reloading → Held or Reloading → SlideBack per GDD Rule 12).
        /// </summary>
        void CompleteReload();
    }
}
