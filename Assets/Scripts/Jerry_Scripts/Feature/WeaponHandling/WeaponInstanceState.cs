namespace JerryScripts.Feature.WeaponHandling
{
    /// <summary>
    /// Finite-state machine values for a weapon instance.
    /// All state transitions are owned by <see cref="WeaponInstance"/>.
    /// </summary>
    /// <remarks>
    /// State graph (see GDD §States and Transitions):
    /// <code>
    /// Holstered --(grab)--> Held
    /// Held --(trigger, ammo>0)--> Firing --> Held
    /// Held --(trigger, ammo==0)--> dry-fire click, stay Held
    /// Held --(primary button)--> Reloading
    /// Held --(drop near mount)--> Holstered
    /// Held --(drop not near mount)--> Dropped
    /// Reloading --(mag inserted, tactical)--> Held
    /// Reloading --(mag inserted, dry)--> SlideBack
    /// SlideBack --(secondary button)--> Held
    /// Dropped --(grab)--> Held
    /// </code>
    /// </remarks>
    public enum WeaponInstanceState
    {
        /// <summary>
        /// Weapon is parented to a rig mount point (hip or chest holster).
        /// Rigidbody is kinematic. Grab is enabled.
        /// </summary>
        Holstered,

        /// <summary>
        /// Weapon is in the player's hand. Firing, aiming, and reload initiation
        /// are all gated on this state being active AND the rig being in
        /// <see cref="JerryScripts.Foundation.RigState.Active"/>.
        /// </summary>
        Held,

        /// <summary>
        /// Transient sub-state of Held. One shot is being processed
        /// (muzzle flash, haptic, recoil kick applied). Returns to
        /// <see cref="Held"/> as soon as the shot-frame completes.
        /// Duration is effectively one physics/render tick.
        /// </summary>
        Firing,

        /// <summary>
        /// Magazine has been dropped. Weapon is waiting for the player to insert
        /// a new magazine into the mag-well socket.
        /// Firing is disabled. Rigidbody remains kinematic (hand still holds the frame).
        /// </summary>
        Reloading,

        /// <summary>
        /// Fresh magazine inserted but first round not yet chambered.
        /// Entered only after a dry reload (previous <c>current_ammo == 0</c>).
        /// Player must press the secondary button to rack the slide and
        /// re-enter <see cref="Held"/>.
        /// </summary>
        SlideBack,

        /// <summary>
        /// Weapon was dropped away from any rig mount point.
        /// Rigidbody is non-kinematic (falls freely). Grab remains enabled.
        /// </summary>
        Dropped
    }
}
