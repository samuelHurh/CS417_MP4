using System;
using UnityEngine;
using JerryScripts.Foundation.Audio;
using JerryScripts.Foundation.Damage;

namespace JerryScripts.Foundation
{
    /// <summary>
    /// Companion component for the hitbox capsule collider created by <see cref="PlayerRig"/>.
    /// Placed on the same child GameObject as the trigger collider.
    ///
    /// <para><b>Responsibilities:</b></para>
    /// <list type="bullet">
    ///   <item>Implements <see cref="IHittable"/> so Sam's enemy projectiles can call
    ///         <c>GetComponent&lt;IHittable&gt;().TakeDamage(dmg)</c> on the layer hit.</item>
    ///   <item>Fires <see cref="OnDamageReceived"/> (float finalDamage) so any subscriber
    ///         (PlayerRig, HUD, death system) can react without coupling to this component.</item>
    ///   <item>Posts <see cref="FeedbackEvent.DamageReceived"/> to
    ///         <see cref="IAudioFeedbackService"/> for the haptic + audio feedback defined
    ///         in <c>FeedbackEventConfig</c> (amp 0.9, dur 0.2 s — GDD catalog row 9).</item>
    /// </list>
    ///
    /// <para><b>Player-source guard:</b> if <see cref="DamageEvent.IsPlayerSource"/> is
    /// <c>true</c> the event is silently ignored. This is a defensive guard; the
    /// hitscan system only calls <see cref="IHittable"/> on the <c>EnemyHitbox</c> layer,
    /// but future analytics or observation patterns might route outgoing events through
    /// any <see cref="IHittable"/> — the guard keeps the player safe in all cases.</para>
    ///
    /// <para>This component does <b>not</b> own a health value. The receiver of
    /// <see cref="OnDamageReceived"/> is responsible for maintaining HP state.</para>
    /// </summary>
    /// <remarks>
    /// S1-008. GDD: player-rig.md §Damage Reception (Rules 13/14); audio-feedback-system.md row 9.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerHitbox : MonoBehaviour, IHittable
    {
        // =====================================================================
        // Events
        // =====================================================================

        /// <summary>
        /// Fired when the player receives damage from an enemy source.
        /// Passes the resolved <c>FinalDamage</c> value (already clamped by
        /// <see cref="IDamageResolver"/>). Zero-alloc: the delegate list is
        /// pre-allocated; repeated invocations do not allocate.
        ///
        /// <para>Subscribed by <see cref="PlayerRig"/> (which re-exposes it) and any
        /// health or death system that needs to track player HP.</para>
        /// </summary>
        public event Action<float> OnDamageReceived;

        // =====================================================================
        // Private — cached references
        // =====================================================================

        /// <summary>
        /// Audio Feedback Service resolved in Awake. Null-safe — haptic/audio dispatch
        /// is silently skipped if the service is absent (headless tests, scenes without
        /// the <c>_Systems</c> GO). Matches the resolution pattern used by WeaponInstance.
        /// </summary>
        private IAudioFeedbackService _audioService;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            // Layer validation — warn once if the GO is not on the expected layer.
            int expectedLayer = LayerMask.NameToLayer("PlayerHitbox");
            if (expectedLayer != -1 && gameObject.layer != expectedLayer)
            {
                Debug.LogWarning(
                    $"[PlayerHitbox] GameObject '{name}' is on layer " +
                    $"'{LayerMask.LayerToName(gameObject.layer)}' but should be on 'PlayerHitbox'. " +
                    "Set the layer in the inspector or let PlayerRig assign it automatically.",
                    this);
            }

            // Resolve audio service once at startup.
            _audioService = FindAnyObjectByType<AudioFeedbackService>();
        }

        // =====================================================================
        // IHittable
        // =====================================================================

        /// <inheritdoc/>
        /// <remarks>
        /// Player-source guard: if <paramref name="dmg"/> was fired by the player
        /// (<see cref="DamageEvent.IsPlayerSource"/> == <c>true</c>), the call is
        /// silently ignored. No log is emitted — this is a defensive guard, not
        /// an error condition.
        /// </remarks>
        public void TakeDamage(in DamageEvent dmg)
        {
            // Defensive guard — player shots must never apply damage to the player.
            // Silent skip: not an error condition (see class-level doc).
            if (dmg.IsPlayerSource) return;

            // Notify subscribers (PlayerRig, health system, HUD).
            OnDamageReceived?.Invoke(dmg.FinalDamage);

            // Post feedback event — AudioFeedbackService owns haptic dispatch (GDD Rule 2).
            // DamageReceived is configured in FeedbackEventConfig (amp 0.9, dur 0.2 s).
            // Position is the hit contact point from the DamageEvent.
            _audioService?.PostFeedbackEvent(new FeedbackEventData(
                FeedbackEvent.DamageReceived,
                dmg.HitPosition,
                dmg.FinalDamage,
                FeedbackHand.None));
        }
    }
}
