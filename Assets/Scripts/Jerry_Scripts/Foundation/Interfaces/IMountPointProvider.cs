using UnityEngine;

namespace JerryScripts.Foundation
{
    /// <summary>
    /// Exposes the six logical attachment points on the player rig used by
    /// weapons, holsters, and other interactables.
    /// All Transforms are child nodes of the XR Origin hierarchy and are
    /// positioned/rotated in the editor via <see cref="PlayerRigConfig"/>.
    /// </summary>
    public interface IMountPointProvider
    {
        /// <summary>Attachment point for the right-hand primary weapon.</summary>
        Transform RightHandMountPoint { get; }

        /// <summary>Attachment point for the left-hand secondary weapon or shield.</summary>
        Transform LeftHandMountPoint { get; }

        /// <summary>Right-hip holster for a sidearm or grenade.</summary>
        Transform RightHipHolster { get; }

        /// <summary>Left-hip holster for a sidearm or grenade.</summary>
        Transform LeftHipHolster { get; }

        /// <summary>Upper-back anchor for a two-handed primary weapon.</summary>
        Transform BackHolster { get; }

        /// <summary>Chest anchor for a third item slot (utility/ammo pouch).</summary>
        Transform ChestMountPoint { get; }
    }
}
