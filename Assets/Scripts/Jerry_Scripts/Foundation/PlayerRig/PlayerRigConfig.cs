using UnityEngine;

namespace JerryScripts.Foundation
{
    /// <summary>
    /// ScriptableObject that holds all designer-tunable parameters for the
    /// player rig. Create one asset via the Assets menu and assign it to the
    /// PlayerRig component in the inspector.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlayerRigConfig",
        menuName  = "CS417/Foundation/Player Rig Config",
        order     = 0)]
    public class PlayerRigConfig : ScriptableObject
    {
        // ---------------------------------------------------------------
        // Tracking
        // ---------------------------------------------------------------

        [Header("Tracking")]
        [Tooltip("Seconds of stable tracking required before the rig transitions " +
                 "from Initializing to Active.")]
        [SerializeField] private float _trackingConfirmDelay = 0.5f;

        /// <summary>Seconds of stable tracking required to leave Initializing state.</summary>
        public float TrackingConfirmDelay => _trackingConfirmDelay;

        // ---------------------------------------------------------------
        // Damage Collider
        // ---------------------------------------------------------------

        [Header("Damage Collider")]
        [Tooltip("Radius (metres) of the capsule damage collider per GDD Rule 13.")]
        [SerializeField] private float _hitboxRadius = 0.25f;

        [Tooltip("Capsule collider height (metres) per GDD Rule 13.")]
        [SerializeField] private float _hitboxHeight = 1.7f;

        [Tooltip("Capsule center Y-offset above XR Origin floor, per GDD Rule 13. " +
                 "Must equal HitboxHeight / 2 to keep the base at floor level.")]
        [SerializeField] private float _hitboxCenterY = 0.85f;

        /// <summary>Radius of the capsule player hitbox in metres.</summary>
        public float HitboxRadius => _hitboxRadius;

        /// <summary>Capsule collider height in metres (GDD Rule 13: 1.7 m).</summary>
        public float HitboxHeight => _hitboxHeight;

        /// <summary>
        /// Y-offset of the capsule centre above the XR Origin floor in metres
        /// (GDD Rule 13: 0.85 m — half of the 1.7 m capsule height).
        /// </summary>
        public float HitboxCenterY => _hitboxCenterY;

        // ---------------------------------------------------------------
        // Locomotion
        // ---------------------------------------------------------------

        [Header("Locomotion")]
        [Tooltip("Movement speed (metres/second) for the continuous move provider.")]
        [SerializeField] private float _moveSpeed = 2.0f;

        [Tooltip("Snap-turn angle in degrees per turn action.")]
        [SerializeField] private float _snapTurnAngle = 45f;

        /// <summary>Continuous movement speed in metres per second.</summary>
        public float MoveSpeed => _moveSpeed;

        /// <summary>Angle in degrees applied per snap-turn input.</summary>
        public float SnapTurnAngle => _snapTurnAngle;
    }
}
