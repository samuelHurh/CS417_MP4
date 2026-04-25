using System;
using UnityEngine;
using UnityEngine.Audio;

namespace JerryScripts.Foundation.Audio
{
    /// <summary>
    /// ScriptableObject that owns all tunable constants for the Audio Feedback Service.
    /// <see cref="AudioFeedbackService"/> reads this at runtime; it never writes to it.
    ///
    /// <para><b>Create via:</b> Assets &gt; Create &gt; CS417 &gt; Foundation &gt; Audio Feedback Config</para>
    ///
    /// <para><b>One asset per project</b> — referenced directly by the
    /// <see cref="AudioFeedbackService"/> MonoBehaviour. If you need per-scene overrides,
    /// add a second config and swap the reference on the service GO.</para>
    /// </summary>
    /// <remarks>
    /// S1-006. GDD: core-fps-weapon-handling.md §Audio &amp; Feedback, §Tuning Knobs.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "FeedbackEventConfig",
        menuName  = "CS417/Foundation/Audio Feedback Config",
        order     = 20)]
    public sealed class FeedbackEventConfig : ScriptableObject
    {
        // =====================================================================
        // Per-event configuration
        // =====================================================================

        /// <summary>
        /// Per-event configuration entry. One entry per <see cref="FeedbackEvent"/> value.
        /// The inspector renders a list — keep entries in the same order as the enum
        /// to make the mapping obvious in the Inspector.
        /// </summary>
        [Serializable]
        public sealed class EventEntry
        {
            [Tooltip("Which FeedbackEvent this entry drives. Must match exactly one enum value.")]
            public FeedbackEvent EventType;

            [Tooltip(
                "Audio clips to choose from via shuffle-bag. " +
                "Leave empty to silence this event (warn + skip on post). " +
                "Add 2–4 variants for natural variation.")]
            public AudioClip[] Clips = Array.Empty<AudioClip>();

            [Tooltip(
                "AudioMixerGroup to route this source through. " +
                "Assign the matching group from the FeedbackAudioMixer asset. " +
                "Null = no mixer group (Unity default output).")]
            public AudioMixerGroup MixerGroup;

            [Tooltip(
                "0 = fully 2D (ignores Position). 1 = fully 3D positional. " +
                "WeaponFire: 1.0. HitConfirmation: 0.0 (ear-space cue). WeaponDryFire: 0.0.")]
            [Range(0f, 1f)]
            public float SpatialBlend = 1f;

            [Tooltip(
                "Volume scale applied to PlayOneShot. " +
                "1.0 = clip's native volume. Reduce for subtler events.")]
            [Range(0f, 1f)]
            public float Volume = 1f;

            [Tooltip(
                "Base pitch before any per-shot randomisation. " +
                "HitConfirmation ignores this — pitch is computed from damage magnitude.")]
            [Range(0.5f, 2f)]
            public float BasePitch = 1f;

            [Tooltip(
                "Random pitch variance added each shot (±cents). " +
                "Applied to WeaponFire and WeaponDryFire. Ignored for HitConfirmation " +
                "(which uses the lerp formula instead). Range: 0–0.15.")]
            [Range(0f, 0.15f)]
            public float PitchVariance = 0.05f;

            [Tooltip(
                "Haptic pulse amplitude (0–1). " +
                "GDD MVP defaults: WeaponFire=0.8, WeaponDryFire=0.3, HitConfirmation=0.0 (no haptic). " +
                "0 = skip haptic for this event.")]
            [Range(0f, 1f)]
            public float HapticAmplitude = 0f;

            [Tooltip(
                "Haptic pulse duration in seconds. " +
                "GDD MVP defaults: WeaponFire=0.04, WeaponDryFire=0.02. " +
                "0 = skip haptic for this event.")]
            [Range(0f, 0.5f)]
            public float HapticDuration = 0f;
        }

        [Header("Per-Event Entries")]
        [Tooltip(
            "One entry per FeedbackEvent enum value. Missing or empty entries are " +
            "warn-and-skip at runtime. Order does not matter — lookup is by EventType field.")]
        [SerializeField] private EventEntry[] _entries = Array.Empty<EventEntry>();

        // =====================================================================
        // HitConfirmation pitch scaling constants
        // =====================================================================

        [Header("HitConfirmation Pitch Scaling")]
        [Tooltip(
            "Damage value at which pitch reaches the ceiling. " +
            "Matches the player damage cap from RarityMultiplierTable. " +
            "GDD default: 330. Changing this here does NOT change the damage cap — " +
            "update RarityMultiplierTable separately.")]
        [Min(1f)]
        [SerializeField] private float _hitConfirmDamageCap = 330f;

        [Tooltip(
            "Minimum pitch for HitConfirmation at zero damage. " +
            "GDD default: 0.9. Safe range: 0.5–1.0.")]
        [Range(0.5f, 1.0f)]
        [SerializeField] private float _hitConfirmPitchFloor = 0.9f;

        [Tooltip(
            "Maximum pitch for HitConfirmation at damage >= cap. " +
            "GDD default: 1.4. Safe range: 1.0–2.0.")]
        [Range(1.0f, 2.0f)]
        [SerializeField] private float _hitConfirmPitchCeiling = 1.4f;

        // =====================================================================
        // Pool sizing
        // =====================================================================

        [Header("AudioSource Pool")]
        [Tooltip(
            "Number of pooled 3D AudioSources pre-warmed at Awake. " +
            "GDD S1-006 default: 8. Raise if you hear audio cutoff on rapid fire.")]
        [Range(4, 32)]
        [SerializeField] private int _poolSize = 8;

        // =====================================================================
        // Public read-only API
        // =====================================================================

        /// <summary>All event entries. Iterated once at startup to build the lookup table.</summary>
        public EventEntry[] Entries => _entries;

        /// <summary>Damage cap for HitConfirmation pitch lerp. GDD default: 330.</summary>
        public float HitConfirmDamageCap => _hitConfirmDamageCap;

        /// <summary>Pitch at zero damage for HitConfirmation. GDD default: 0.9.</summary>
        public float HitConfirmPitchFloor => _hitConfirmPitchFloor;

        /// <summary>Pitch at max damage for HitConfirmation. GDD default: 1.4.</summary>
        public float HitConfirmPitchCeiling => _hitConfirmPitchCeiling;

        /// <summary>Number of 3D AudioSources to pre-warm in the pool.</summary>
        public int PoolSize => _poolSize;

        // =====================================================================
        // Lookup helper
        // =====================================================================

        /// <summary>
        /// Finds the config entry for the given event type.
        /// Returns <c>null</c> if no matching entry exists.
        /// Linear scan — called at runtime only; array is small (3 events wired in MVP, 19 declared).
        /// </summary>
        public EventEntry Find(FeedbackEvent eventType)
        {
            if (_entries == null) return null;
            foreach (EventEntry entry in _entries)
            {
                if (entry != null && entry.EventType == eventType)
                    return entry;
            }
            return null;
        }
    }
}
