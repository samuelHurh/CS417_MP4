using BNG;
using JerryScripts.Core.PlayerState;
using JerryScripts.Foundation.Audio;
using UnityEngine;

/// <summary>
/// Single-use healing pickup. Spawned by <see cref="PurchaseStation"/> in the shop room.
/// On first grab by either hand, applies a flat heal to the player and destroys itself.
///
/// <para><b>Wiring</b>: attach to a prefab that also has a <see cref="Grabbable"/>
/// + a Collider. The grabbable lets the player physically pick it up.</para>
///
/// <para><b>Asmdef boundary note</b>: lives in default Assembly-CSharp because it
/// references BNG <see cref="Grabbable"/>.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Grabbable))]
public sealed class HealingItem : MonoBehaviour
{
    [Header("Heal")]
    [Tooltip("HP restored on grab. Clamped to player's MaxHealth by PSM.ApplyHeal.")]
    [Min(0f)]
    [SerializeField] private float _healAmount = 50f;

    private Grabbable _grabbable;
    private bool _consumed;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
    }

    private void Update()
    {
        if (_consumed) return;
        if (_grabbable == null) return;
        if (!_grabbable.BeingHeld) return;

        _consumed = true;

        var psm = FindAnyObjectByType<PlayerStateManager>();
        if (psm != null)
        {
            psm.ApplyHeal(_healAmount);
        }

        // Reuse CurrencyPickup audio for the consume confirmation. Could split into
        // a dedicated FeedbackEvent.HealingPickup later if desired.
        var audio = FindAnyObjectByType<AudioFeedbackService>();
        audio?.PostFeedbackEvent(new FeedbackEventData(
            FeedbackEvent.CurrencyPickup,
            transform.position,
            0f,
            FeedbackHand.None));

        // Release the Grabbable before destroying so the holding Grabber's hand pose
        // resets correctly. Otherwise the hand stays stuck in the grab pose because
        // Destroy() bypasses BNG's normal release event chain. Same pattern as
        // BNG's own Damageable.DestroyThis() drop-on-death path.
        if (_grabbable != null && _grabbable.BeingHeld)
        {
            _grabbable.DropItem(false, true);
        }

        Destroy(gameObject);
    }
}
