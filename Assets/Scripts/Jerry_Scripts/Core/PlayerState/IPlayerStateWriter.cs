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
        /// Restores HP. Clamps to <c>[CurrentHealth, MaxHealth]</c> — never overheals,
        /// never reduces. No-op when player is <see cref="PlayerState.Dead"/>
        /// (no resurrection from heal items). Negative, zero, and NaN values rejected.
        /// </summary>
        /// <param name="amount">HP to restore. Must be positive non-NaN.</param>
        void ApplyHeal(float amount);

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
        /// Atomic check-and-deduct. Returns true and decrements <see cref="IPlayerStateReader.CurrentCurrency"/>
        /// by <paramref name="amount"/> if the player has at least that much; returns false
        /// without modifying state otherwise. Negative or zero amounts silently return false.
        ///
        /// <para>Used by the shop-room purchase flow to commit a purchase only when affordable.</para>
        /// </summary>
        /// <param name="amount">Currency to deduct. Must be positive.</param>
        /// <returns>True if the deduction succeeded; false if insufficient funds.</returns>
        bool SpendCurrency(int amount);

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
