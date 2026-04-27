namespace JerryScripts.Core.PlayerState
{
    /// <summary>
    /// High-level states for the player state machine.
    ///
    /// <para>These states are orthogonal to <see cref="RigState"/>: <c>RigState</c>
    /// tracks headset/tracking lifecycle; <c>PlayerState</c> tracks gameplay lifecycle
    /// (alive, dead, between states). They are kept separate so the PSM does not need
    /// to import XR subsystem types.</para>
    ///
    /// <para>State transition rules (enforced by <see cref="PlayerStateManager"/>):</para>
    /// <list type="bullet">
    ///   <item><c>Running</c> → <c>Dead</c> when HP drops to or below zero.</item>
    ///   <item><c>Dead</c> → <c>Running</c> via <see cref="IPlayerStateWriter.RequestRestart"/>.</item>
    ///   <item><c>Running</c> → <c>Paused</c> / <c>Paused</c> → <c>Running</c> via
    ///         <see cref="RigState"/> relay (PlayerRig owns the pause toggle).</item>
    /// </list>
    /// </summary>
    /// <remarks>S2-002. GDD: player-state-management.md §State Machine.</remarks>
    public enum PlayerState
    {
        /// <summary>
        /// Normal gameplay is running. Player can move, shoot, and take damage.
        /// HP and currency tick normally.
        /// </summary>
        Running = 0,

        /// <summary>
        /// Gameplay is suspended by the pause menu. HP and currency are frozen.
        /// Locomotion is disabled (managed by <see cref="PlayerRig"/>).
        /// </summary>
        Paused = 1,

        /// <summary>
        /// Player HP reached zero. All input is locked.
        /// Transitions to <c>Running</c> only via <see cref="IPlayerStateWriter.RequestRestart"/>.
        /// </summary>
        Dead = 2,
    }
}
