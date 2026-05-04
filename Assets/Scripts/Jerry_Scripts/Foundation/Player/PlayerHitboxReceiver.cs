using JerryScripts.Core.PlayerState;
using JerryScripts.Foundation.Audio;
using JerryScripts.Foundation.Damage;
using UnityEngine;

namespace JerryScripts.Foundation.Player
{
        /// <summary>
        /// Receives damage from Sam's enemy attack flow and forwards it to
        /// <see cref="PlayerStateManager.ApplyDamage"/>. Replaces the deprecated
        /// <c>PlayerHitbox</c> + <c>PlayerRig.OnDamageReceived</c> path.
        ///
        /// <para><b>Wiring</b>: attach to a child GameObject of the BNG XR Rig Advanced
        /// that has a Collider on the <c>PlayerHitbox</c> layer. Sam's
        /// <c>PlayerDamageHelpers.TryDamagePlayer</c> walks parent colliders looking
        /// for an <see cref="IHittable"/>, finds this component, and calls
        /// <see cref="TakeDamage"/>.</para>
        ///
        /// <para><b>Death-state guard</b>: silently drops all damage events while the
        /// player is in <see cref="PlayerState.Dead"/>.</para>
        ///
        /// <para><b>Invincibility frames</b>: after a successful damage event, blocks
        /// both subsequent damage application AND <c>DamageReceived</c> audio for
        /// <see cref="IFrameDurationSeconds"/>. Prevents same-bullet multi-hit
        /// (Sam's <c>bullet.cs</c> isn't destroyed on impact and has both
        /// OnCollisionEnter + OnTriggerEnter handlers) and AOE tick spam.</para>
        ///
        /// <para><b>Killing-blow guard</b>: if the damage transitions the player to Dead,
        /// <c>DamageReceived</c> is suppressed so it doesn't overlap with
        /// <see cref="FeedbackEvent.PlayerDeath"/> audio fired by PSM.</para>
        /// </summary>
        /// <remarks>Sprint Final, Phase 1 + i-frames. Replaces <c>Foundation/PlayerRig/PlayerHitbox.cs</c>.</remarks>
        [DisallowMultipleComponent]
        public sealed class PlayerHitboxReceiver : MonoBehaviour, IHittable
        {
                /// <summary>Seconds of full invincibility after a damage event lands.
                /// Tune lower for faster pacing; raise if multi-enemy attacks feel unfair.</summary>
                private const float IFrameDurationSeconds = 1.0f;

                private IPlayerStateReader _stateReader;
                private IPlayerStateWriter _stateWriter;
                private IAudioFeedbackService _audioService;
                private float _lastDamageTime = -10f;

                private void OnEnable()
                {
                        var psm = FindAnyObjectByType<PlayerStateManager>();
                        _stateReader = psm;
                        _stateWriter = psm;
                        _audioService = FindAnyObjectByType<AudioFeedbackService>();

                        if (psm == null)
                        {
                                Debug.LogWarning(
                                        "[PlayerHitboxReceiver] No PlayerStateManager in scene — damage will be silently dropped. " +
                                        "Add a PlayerStateManager component to the _Systems GameObject.",
                                        this);
                        }
                }

                /// <inheritdoc/>
                public void TakeDamage(in DamageEvent dmg)
                {
                        if (dmg.IsPlayerSource) return;

                        // Skip everything (damage AND audio) if player is already dead.
                        if (_stateReader != null && _stateReader.CurrentState == PlayerState.Dead) return;

                        // I-frame guard: skip the entire damage event (no HP change, no audio)
                        // if we're still within the post-hit invincibility window.
                        // Use Time.unscaledTime so the window stays correct when timeScale = 0.
                        float now = Time.unscaledTime;
                        if (now - _lastDamageTime < IFrameDurationSeconds) return;
                        _lastDamageTime = now;

                        // Apply damage. State may transition to Dead in the same call.
                        _stateWriter?.ApplyDamage(dmg.FinalDamage);

                        // Killing-blow guard: PSM has already posted PlayerDeath audio synchronously.
                        // Don't double up with DamageReceived.
                        if (_stateReader != null && _stateReader.CurrentState == PlayerState.Dead) return;

                        _audioService?.PostFeedbackEvent(new FeedbackEventData(
                                FeedbackEvent.DamageReceived,
                                dmg.HitPosition,
                                dmg.FinalDamage,
                                FeedbackHand.None));
                }
        }
}
