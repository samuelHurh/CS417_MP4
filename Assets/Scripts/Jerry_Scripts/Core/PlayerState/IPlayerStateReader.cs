using System;

namespace JerryScripts.Core.PlayerState
{
    /// <summary>
    /// Read-only view of the player state machine.
    ///
    /// <para>UI, audio, and other observers subscribe to the events here rather
    /// than polling every frame. The HUD, death screen, and pause screen all
    /// operate through this interface.</para>
    ///
    /// <para>All currency values are non-negative integers. HP values are in the
    /// range <c>[0, MaxHealth]</c>.</para>
    /// </summary>
    /// <remarks>S2-002. GDD: player-state-management.md §IPlayerStateReader.</remarks>
    public interface IPlayerStateReader
    {
        // -------------------------------------------------------------------
        // Snapshot properties
        // -------------------------------------------------------------------

        /// <summary>Current player gameplay state.</summary>
        PlayerState CurrentState { get; }

        /// <summary>Current health points. Always in <c>[0, MaxHealth]</c>.</summary>
        float CurrentHealth { get; }

        /// <summary>
        /// Maximum health points as configured in <see cref="PlayerStateConfig"/>.
        /// Constant for a run; does not change without a restart.
        /// </summary>
        float MaxHealth { get; }

        /// <summary>
        /// Current currency balance. Always &gt;= 0.
        /// Incremented by <see cref="IPlayerStateWriter.AddCurrency"/>.
        /// </summary>
        int CurrentCurrency { get; }

        // -------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------

        /// <summary>
        /// Fired whenever <see cref="CurrentState"/> changes.
        /// Argument: the new <see cref="PlayerState"/> the system has entered.
        ///
        /// <para>Subscribers should not assume the previous state — check
        /// <see cref="CurrentState"/> after receiving this event.</para>
        /// </summary>
        event Action<PlayerState> OnStateChanged;

        /// <summary>
        /// Fired whenever <see cref="CurrentHealth"/> changes.
        /// Argument: the new health value (already clamped to <c>[0, MaxHealth]</c>).
        ///
        /// <para>Fired on every damage or heal call, including when the value does
        /// not cross a meaningful threshold — the HUD subscriber is responsible
        /// for filtering noise if needed.</para>
        /// </summary>
        event Action<float> OnHealthChanged;

        /// <summary>
        /// Fired whenever <see cref="CurrentCurrency"/> changes.
        /// Argument: the new currency balance.
        /// </summary>
        event Action<int> OnCurrencyChanged;

        /// <summary>
        /// Fired once when HP drops to zero and the state transitions to
        /// <see cref="PlayerState.Dead"/>. Fires before <see cref="OnStateChanged"/>.
        ///
        /// <para>Subscribers include: <see cref="PlayerRig.Die()"/> relay,
        /// death-screen activator, audio death sting.</para>
        /// </summary>
        event Action OnDeathConfirmed;
    }
}
