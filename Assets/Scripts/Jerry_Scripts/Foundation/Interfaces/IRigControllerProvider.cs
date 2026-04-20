using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace JerryScripts.Foundation
{
    /// <summary>
    /// Provides access to the physical controller transforms and haptic players
    /// on the player rig. Use this interface rather than caching the concrete
    /// <see cref="PlayerRig"/> to keep systems decoupled.
    /// </summary>
    public interface IRigControllerProvider
    {
        /// <summary>Transform of the left VR controller.</summary>
        Transform LeftControllerTransform { get; }

        /// <summary>Transform of the right VR controller.</summary>
        Transform RightControllerTransform { get; }

        /// <summary>
        /// Haptic impulse player for the left controller.
        /// Call <c>SendHapticImpulse(amplitude, duration)</c> to trigger rumble.
        /// </summary>
        HapticImpulsePlayer LeftHaptics { get; }

        /// <summary>
        /// Haptic impulse player for the right controller.
        /// Call <c>SendHapticImpulse(amplitude, duration)</c> to trigger rumble.
        /// </summary>
        HapticImpulsePlayer RightHaptics { get; }
    }
}
