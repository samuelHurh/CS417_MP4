namespace JerryScripts.Core.PlayerState
{
    /// <summary>
    /// Write interface for the player state machine.
    ///
    /// <para>Only systems with a legitimate reason to mutate player state should
    /// hold a reference to this interface. UI buttons (Restart, Quit), the
    /// currency/pickup system, and hitbox receivers are the primary callers.</para>
    ///
    /// <para>Healing is out of scope for MVP.</para>
    /// </summary>
    /// <remarks>S2-002. GDD: player-state-management.md §IPlayerStateWriter.</remarks>
    public interface IPlayerStateWriter
    {
        /// <summary>
        /// Applies damage to the player's HP. Clamps to [0, MaxHealth].
        /// Fires <see cref="IPlayerStateReader.OnHealthChanged"/> on actual change.
        /// Triggers death sequence (OnDeathConfirmed + state transition to Dead) at HP = 0.
        /// No-op if already Dead. Negative or NaN amounts silently rejected.
        ///
        /// <para>Called by hitbox receivers (e.g. <c>PlayerHitboxReceiver</c>) instead of
        /// the deprecated PlayerRig event subscription path.</para>
        /// </summary>
        /// <param name="amount">Damage to subtract from current HP. Must be positive non-NaN.</param>
        void ApplyDamage(float amount);

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
        /// Transitions to <see cref="PlayerState.Paused"/> when currently
        /// <see cref="PlayerState.Running"/>. No-op from any other state.
        /// Pair with <see cref="RequestResume"/> for the unpause direction.
        /// </summary>
        void RequestPause();

        /// <summary>
        /// Resumes gameplay from the <see cref="PlayerState.Paused"/> state.
        /// Transitions to <see cref="PlayerState.Running"/>.
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
