namespace JerryScripts.Core.PlayerState
{
    /// <summary>
    /// Write interface for the player state machine.
    ///
    /// <para>Only systems with a legitimate reason to mutate player state should
    /// hold a reference to this interface. UI buttons (Restart, Quit) and the
    /// currency/pickup system are the primary callers.</para>
    ///
    /// <para>Health mutation is intentionally excluded: HP is driven exclusively
    /// by subscribing to <see cref="PlayerRig.OnDamageReceived"/>. Healing is out
    /// of scope for MVP.</para>
    /// </summary>
    /// <remarks>S2-002. GDD: player-state-management.md §IPlayerStateWriter.</remarks>
    public interface IPlayerStateWriter
    {
        /// <summary>
        /// Adds <paramref name="amount"/> to the player's currency balance.
        /// <paramref name="amount"/> must be &gt; 0; negative values are silently clamped
        /// to zero (spending is handled by a separate purchase system, not this interface).
        ///
        /// <para>Fires <see cref="IPlayerStateReader.OnCurrencyChanged"/> with the new balance.</para>
        /// </summary>
        /// <param name="amount">Positive integer to add to the balance.</param>
        void AddCurrency(int amount);

        /// <summary>
        /// Resets HP to <see cref="IPlayerStateReader.MaxHealth"/>, clears currency,
        /// and transitions to <see cref="PlayerState.Running"/>.
        ///
        /// <para>No-op if the player is already in <see cref="PlayerState.Running"/>.</para>
        /// <para>Fires <see cref="IPlayerStateReader.OnHealthChanged"/>,
        /// <see cref="IPlayerStateReader.OnCurrencyChanged"/>, and
        /// <see cref="IPlayerStateReader.OnStateChanged"/> in that order.</para>
        /// </summary>
        void RequestRestart();

        /// <summary>
        /// Resumes gameplay from the <see cref="PlayerState.Paused"/> state.
        /// Transitions to <see cref="PlayerState.Running"/> and tells the rig to
        /// return to <see cref="RigState.Active"/>.
        /// No-op if not currently paused.
        /// </summary>
        void RequestResume();

        /// <summary>
        /// Requests an application quit. Calls <c>Application.Quit()</c> in a player
        /// build; a no-op in the Editor (Unity does not support quitting in-editor).
        /// </summary>
        void RequestQuit();
    }
}
