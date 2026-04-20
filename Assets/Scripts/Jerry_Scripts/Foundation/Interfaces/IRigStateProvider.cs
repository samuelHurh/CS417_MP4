using System;

namespace JerryScripts.Foundation
{
    /// <summary>
    /// Possible lifecycle states for the player rig.
    /// </summary>
    public enum RigState
    {
        /// <summary>Rig is performing first-frame setup; tracking not yet confirmed.</summary>
        Initializing,

        /// <summary>Tracking confirmed; normal gameplay is running.</summary>
        Active,

        /// <summary>Gameplay is suspended (pause menu open, etc.).</summary>
        Paused,

        /// <summary>Player health reached zero; rig is locked.</summary>
        Dead,

        /// <summary>
        /// Player is mid-room-transition; all input blocked by Room Management.
        /// Locomotion is disabled for the duration of the transition.
        /// </summary>
        Transitioning
    }

    /// <summary>
    /// Read-only view of the rig's finite-state machine.
    /// Systems that need to react to rig-state changes subscribe to
    /// <see cref="OnStateChanged"/> rather than polling every frame.
    /// </summary>
    public interface IRigStateProvider
    {
        /// <summary>Current rig state.</summary>
        RigState CurrentState { get; }

        /// <summary>
        /// Fired whenever the rig transitions between states.
        /// Argument: the new state the rig has entered.
        /// </summary>
        event Action<RigState> OnStateChanged;

        /// <summary>
        /// Fired exactly once when the rig first transitions from
        /// <see cref="RigState.Initializing"/> to <see cref="RigState.Active"/>.
        /// Subscribe here for one-time post-init setup.
        /// </summary>
        event Action OnRigReady;

        /// <summary>
        /// Fired when the rig enters <see cref="RigState.Dead"/>.
        /// Use this to trigger death-screen, respawn flow, etc.
        /// </summary>
        event Action OnRigDeactivated;
    }
}
