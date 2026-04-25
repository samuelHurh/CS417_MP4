using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace JerryScripts.Foundation.Audio
{
    /// <summary>
    /// MonoBehaviour implementation of <see cref="IAudioFeedbackService"/>.
    ///
    /// <para><b>Place one instance on a <c>_Systems</c> GameObject in the scene.</b>
    /// Feature/Core layer systems resolve it via <c>FindAnyObjectByType&lt;AudioFeedbackService&gt;()</c>
    /// in their <c>Awake</c>; this matches the existing <c>PlayerRig</c> / <c>ProjectileSystem</c>
    /// resolution pattern and requires no inspector wiring on callers.</para>
    ///
    /// <para><b>Audio pool:</b> 8 child GameObjects are created at <c>Awake</c>, each with
    /// a 3D <see cref="AudioSource"/> component. Pool acquisition uses round-robin selection.
    /// One additional non-pooled 2D <see cref="AudioSource"/> on this GameObject handles
    /// events with <c>SpatialBlend == 0</c> (e.g., <c>HitConfirmation</c>).</para>
    ///
    /// <para><b>Null-safe on missing clips:</b> if a config entry exists but has no clips
    /// (or all clips are null), the service logs a warning once and returns without playing.
    /// <c>AudioSource.PlayOneShot(null)</c> would not throw, but we guard explicitly to
    /// emit a useful diagnostic the first time a clip is missing.</para>
    ///
    /// <para><b>Pitch scaling — HitConfirmation:</b> pitch is determined by
    /// <see cref="CalculateHitConfirmationPitch"/> (a <c>static</c> pure helper so it is
    /// unit-testable without a scene). All other events apply a small ±cents random variance
    /// from the config entry's <c>PitchVariance</c> field.</para>
    /// </summary>
    /// <remarks>
    /// S1-006. GDD: core-fps-weapon-handling.md §Audio &amp; Feedback.
    /// Wiring checklist: see <c>Foundation/Audio/README.md</c>.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AudioFeedbackService : MonoBehaviour, IAudioFeedbackService
    {
        // =====================================================================
        // Inspector fields
        // =====================================================================

        [Header("Config")]
        [Tooltip(
            "ScriptableObject asset containing per-event clip arrays, mixer groups, " +
            "pitch/volume tuning, and the HitConfirmation pitch formula constants. " +
            "Create via Assets > Create > CS417 > Foundation > Audio Feedback Config.")]
        [SerializeField] private FeedbackEventConfig _config;

        // =====================================================================
        // Private — pool
        // =====================================================================

        /// <summary>Round-robin cursor for pool acquisition.</summary>
        private int _poolCursor;

        /// <summary>Pre-warmed 3D AudioSources (child GameObjects).</summary>
        private AudioSource[] _pool;

        /// <summary>Non-pooled 2D AudioSource on this GO for ear-space events (SpatialBlend == 0).</summary>
        private AudioSource _source2D;

        // =====================================================================
        // Private — lookup
        // =====================================================================

        /// <summary>
        /// Event type → (shuffle bag, config entry) map.
        /// Built once in <c>Awake</c> from <see cref="FeedbackEventConfig.Entries"/>.
        /// </summary>
        private readonly Dictionary<FeedbackEvent, (ShuffleBag<AudioClip> Bag, FeedbackEventConfig.EventEntry Entry)>
            _lookup = new Dictionary<FeedbackEvent, (ShuffleBag<AudioClip>, FeedbackEventConfig.EventEntry)>();

        /// <summary>
        /// Tracks event types that have already fired a "no config entry" warning.
        /// Prevents console spam when a future-story event is posted every frame before
        /// its <see cref="FeedbackEventConfig.EventEntry"/> is wired up.
        /// </summary>
        private readonly HashSet<FeedbackEvent> _warnedMissingConfig = new HashSet<FeedbackEvent>();

        // =====================================================================
        // Private — haptic
        // =====================================================================

        /// <summary>
        /// Cached haptic provider resolved from the scene's <see cref="PlayerRig"/> in <c>Awake</c>.
        /// Null-safe — haptic dispatch is skipped if no rig is present (headless tests, no-VR sessions).
        /// Per GDD Rule 2: only this service calls <c>SendHapticImpulse</c>.
        /// </summary>
        private IRigControllerProvider _rigControllerProvider;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            BuildPool();
            Build2DSource();
            BuildLookup();

            // Resolve haptic provider from the scene's PlayerRig (same pattern as WeaponInstance).
            // FindAnyObjectByType runs once at startup — cheap for a single-player VR scene.
            // Cast is null-safe: if PlayerRig is absent (headless tests), _rigControllerProvider stays null.
            _rigControllerProvider = FindAnyObjectByType<PlayerRig>();
        }

        // =====================================================================
        // IAudioFeedbackService
        // =====================================================================

        /// <inheritdoc/>
        public void PostFeedbackEvent(FeedbackEventData data)
        {
            if (!_lookup.TryGetValue(data.EventType, out var pair))
            {
                // Warn only once per event type — avoids console spam for future-story events
                // that are posted before their config entry is wired (e.g., events posted per-frame).
                if (_warnedMissingConfig.Add(data.EventType))
                {
                    Debug.LogWarning(
                        $"[AudioFeedbackService] No config entry for FeedbackEvent.{data.EventType}. " +
                        "Add an EventEntry in the FeedbackEventConfig asset.",
                        this);
                }
                return;
            }

            FeedbackEventConfig.EventEntry entry = pair.Entry;
            ShuffleBag<AudioClip>          bag   = pair.Bag;

            // Null/empty clip guard — warn once per missing-clip event, skip play
            AudioClip clip = bag?.Next();
            if (clip == null)
            {
                Debug.LogWarning(
                    $"[AudioFeedbackService] FeedbackEvent.{data.EventType} has no valid AudioClips. " +
                    "Assign clips in the FeedbackEventConfig asset to hear this event.",
                    this);
                return;
            }

            float pitch = ComputePitch(data, entry);

            if (entry.SpatialBlend > 0f)
            {
                // 3D positional — borrow a pooled source
                AudioSource source = AcquirePooledSource();
                source.transform.position = data.Position;
                source.spatialBlend       = entry.SpatialBlend;
                source.outputAudioMixerGroup = entry.MixerGroup;
                source.pitch              = pitch;
                source.PlayOneShot(clip, entry.Volume);
            }
            else
            {
                // 2D ear-space — use the dedicated non-pooled source on this GO
                _source2D.outputAudioMixerGroup = entry.MixerGroup;
                _source2D.pitch                 = pitch;
                _source2D.PlayOneShot(clip, entry.Volume);
            }

            // Haptic dispatch — GDD Rule 2: only AudioFeedbackService calls SendHapticImpulse.
            // Guard: skip if amplitude/duration are zero OR if the rig provider is absent
            // (headless tests, no-VR sessions). S1-006.
            if (entry.HapticAmplitude > 0f && entry.HapticDuration > 0f && _rigControllerProvider != null)
            {
                switch (data.Hand)
                {
                    case FeedbackHand.Left:
                        _rigControllerProvider.LeftHaptics?.SendHapticImpulse(entry.HapticAmplitude, entry.HapticDuration);
                        break;
                    case FeedbackHand.Right:
                        _rigControllerProvider.RightHaptics?.SendHapticImpulse(entry.HapticAmplitude, entry.HapticDuration);
                        break;
                    case FeedbackHand.Both:
                        _rigControllerProvider.LeftHaptics?.SendHapticImpulse(entry.HapticAmplitude, entry.HapticDuration);
                        _rigControllerProvider.RightHaptics?.SendHapticImpulse(entry.HapticAmplitude, entry.HapticDuration);
                        break;
                    case FeedbackHand.None:
                    default:
                        break;
                }
            }
        }

        // =====================================================================
        // Pitch computation
        // =====================================================================

        /// <summary>
        /// Returns the correct playback pitch for the given event.
        /// <see cref="FeedbackEvent.HitConfirmation"/> uses the damage-lerp formula.
        /// All other events apply a small random ±variance around the entry's base pitch.
        /// </summary>
        private float ComputePitch(in FeedbackEventData data, FeedbackEventConfig.EventEntry entry)
        {
            if (data.EventType == FeedbackEvent.HitConfirmation && _config != null)
            {
                return CalculateHitConfirmationPitch(
                    data.Magnitude,
                    _config.HitConfirmDamageCap,
                    _config.HitConfirmPitchFloor,
                    _config.HitConfirmPitchCeiling);
            }

            // Random ±variance for WeaponFire, WeaponDryFire, etc.
            float variance = UnityEngine.Random.Range(-entry.PitchVariance, entry.PitchVariance);
            return entry.BasePitch + variance;
        }

        // =====================================================================
        // Pitch helper — static so unit tests can call it without a scene
        // =====================================================================

        /// <summary>
        /// Computes HitConfirmation playback pitch from <paramref name="finalDamage"/>.
        /// Pitch is linearly interpolated from <paramref name="pitchFloor"/> (at 0 damage)
        /// to <paramref name="pitchCeiling"/> (at <paramref name="damageCap"/> or above).
        ///
        /// <para><b>Formula:</b> <c>Lerp(floor, ceiling, InverseLerp(0, cap, finalDamage))</c>.
        /// <c>Mathf.Lerp</c> clamps t to [0,1] — no explicit clamp needed.</para>
        /// </summary>
        /// <param name="finalDamage">Resolved damage from <c>DamageEvent.FinalDamage</c>.</param>
        /// <param name="damageCap">Damage value that maps to pitch ceiling. GDD default: 330.</param>
        /// <param name="pitchFloor">Pitch at zero damage. GDD default: 0.9.</param>
        /// <param name="pitchCeiling">Pitch at max damage. GDD default: 1.4.</param>
        /// <returns>Clamped pitch in [<paramref name="pitchFloor"/>, <paramref name="pitchCeiling"/>].</returns>
        internal static float CalculateHitConfirmationPitch(
            float finalDamage,
            float damageCap,
            float pitchFloor,
            float pitchCeiling)
        {
            float t = Mathf.InverseLerp(0f, damageCap, finalDamage);
            return Mathf.Lerp(pitchFloor, pitchCeiling, t);
        }

        // =====================================================================
        // Pool management
        // =====================================================================

        /// <summary>Pre-warms the 3D AudioSource pool per config pool size.</summary>
        private void BuildPool()
        {
            int size = _config != null ? _config.PoolSize : 8;
            _pool = new AudioSource[size];

            for (int i = 0; i < size; i++)
            {
                var child = new GameObject($"FeedbackAudioSource_{i:D2}");
                child.transform.SetParent(transform, worldPositionStays: false);

                AudioSource src = child.AddComponent<AudioSource>();
                src.playOnAwake  = false;
                src.spatialBlend = 1f;  // default 3D; overridden per-play
                src.rolloffMode  = AudioRolloffMode.Linear;
                src.minDistance  = 1f;
                src.maxDistance  = 30f;

                _pool[i] = src;
            }
        }

        /// <summary>Creates the non-pooled 2D source on this GameObject.</summary>
        private void Build2DSource()
        {
            _source2D              = gameObject.AddComponent<AudioSource>();
            _source2D.playOnAwake  = false;
            _source2D.spatialBlend = 0f;  // fully 2D
        }

        /// <summary>
        /// Round-robin pool acquisition.
        /// Simple and deterministic — no "oldest finished" polling.
        /// With 8 sources and shots at max fire rate (900 RPM = 15 Hz),
        /// each source has 533 ms between reuse, which is longer than any
        /// gunshot clip in scope. Raise pool size if rapid-fire causes cutoff.
        /// </summary>
        private AudioSource AcquirePooledSource()
        {
            AudioSource source = _pool[_poolCursor];
            _poolCursor = (_poolCursor + 1) % _pool.Length;
            return source;
        }

        // =====================================================================
        // Lookup table construction
        // =====================================================================

        /// <summary>
        /// Builds the event-type → (bag, entry) lookup from config entries.
        /// Logs a warning for any <see cref="FeedbackEvent"/> value with no matching entry.
        /// Lazy shuffle bags are initialised now (one bag per event type).
        /// </summary>
        private void BuildLookup()
        {
            _lookup.Clear();

            if (_config == null)
            {
                Debug.LogError(
                    "[AudioFeedbackService] FeedbackEventConfig is not assigned. " +
                    "All PostFeedbackEvent calls will be silently skipped. " +
                    "Assign the config asset in the Inspector.",
                    this);
                return;
            }

            if (_config.Entries == null) return;

            foreach (FeedbackEventConfig.EventEntry entry in _config.Entries)
            {
                if (entry == null) continue;

                // Filter null clips from the bag — PlayOneShot(null) is silent but
                // we want the bag to represent only valid, playable clips.
                AudioClip[] validClips = FilterNullClips(entry.Clips);

                ShuffleBag<AudioClip> bag = validClips.Length > 0
                    ? new ShuffleBag<AudioClip>(validClips)
                    : null;  // null bag → warn-and-skip in PostFeedbackEvent

                if (_lookup.ContainsKey(entry.EventType))
                {
                    Debug.LogWarning(
                        $"[AudioFeedbackService] Duplicate EventEntry for FeedbackEvent.{entry.EventType}. " +
                        "Only the first entry will be used.",
                        this);
                    continue;
                }

                _lookup[entry.EventType] = (bag, entry);
            }

            // Warn about any enum values that have no config entry (helpful during development)
            foreach (FeedbackEvent value in Enum.GetValues(typeof(FeedbackEvent)))
            {
                if (!_lookup.ContainsKey(value))
                {
                    Debug.LogWarning(
                        $"[AudioFeedbackService] No EventEntry configured for FeedbackEvent.{value}. " +
                        "Posting this event will warn-and-skip at runtime.",
                        this);
                }
            }
        }

        /// <summary>Returns a new array containing only non-null elements from <paramref name="clips"/>.</summary>
        private static AudioClip[] FilterNullClips(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return Array.Empty<AudioClip>();

            int validCount = 0;
            foreach (AudioClip c in clips)
                if (c != null) validCount++;

            if (validCount == clips.Length) return clips;  // fast path — no nulls

            AudioClip[] filtered = new AudioClip[validCount];
            int idx = 0;
            foreach (AudioClip c in clips)
                if (c != null) filtered[idx++] = c;

            return filtered;
        }
    }
}
