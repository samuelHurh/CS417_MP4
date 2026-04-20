using UnityEngine;

namespace JerryScripts.Foundation
{
    /// <summary>
    /// Companion component for the hitbox sphere collider created by <see cref="PlayerRig"/>.
    /// Placed on the same head GameObject as the trigger collider.
    ///
    /// Other systems that deal damage to the player should:
    ///   1. Detect the trigger on the PlayerHitbox layer.
    ///   2. Call <c>GetComponentInParent&lt;IDamageable&gt;()</c> to reach the health system.
    ///
    /// This component itself does not apply damage — it is purely an identity marker
    /// so that damage systems can distinguish a player hit from other triggers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHitbox : MonoBehaviour
    {
        // Intentionally left minimal. The PlayerRig creates the CapsuleCollider
        // at runtime on a child GO of the XR Origin root (per GDD Rule 13/14);
        // this script tags the GameObject as the damage receiver entry point.

        private void Awake()
        {
            int expectedLayer = LayerMask.NameToLayer("PlayerHitbox");
            if (expectedLayer != -1 && gameObject.layer != expectedLayer)
            {
                Debug.LogWarning(
                    $"[PlayerHitbox] GameObject '{name}' is on layer '{LayerMask.LayerToName(gameObject.layer)}' " +
                    $"but should be on 'PlayerHitbox'. Set the layer in the inspector or let PlayerRig assign it automatically.",
                    this);
            }
        }
    }
}
