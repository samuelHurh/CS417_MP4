using UnityEngine;

namespace JerryScripts.Foundation.Audio
{
    // =========================================================================
    // FeedbackEvent — event type enum
    // =========================================================================

    /// <summary>
    /// Identifies which feedback event to play.
    /// Every value must have a corresponding entry in <see cref="FeedbackEventConfig"/>
    /// or the service will log a warning and skip playback.
    ///
    /// <para>When adding a new value: add it here, add a matching
    /// <see cref="FeedbackEventConfig.EventEntry"/> in the config asset, add an
    /// AudioClip array in the Project, and add a test guard in
    /// <c>AudioFeedbackServiceTests</c>.</para>
    ///
    /// <para>All 19 values from the GDD §Feedback Event Catalog are declared here as a
    /// forward-compatibility contract. Values 2–18 are not yet wired to config entries;
    /// the service will warn-and-skip those events until their stories are implemented.</para>
    /// </summary>
    /// <remarks>S1-006. GDD: audio-feedback-system.md §Feedback Event Catalog.</remarks>
    public enum FeedbackEvent
    {
        /// <summary>3D gunshot played at muzzle position on weapon fire.</summary>
        WeaponFire = 0,

        /// <summary>
        /// Dry-fire click played when trigger is pulled with 0 ammo.
        /// S1-010 (Should-Have). Wired when story is implemented.
        /// </summary>
        WeaponDryFire = 1,

        /// <summary>(Future story) Magazine release — physical mag drop sound.</summary>
        MagDrop = 2,

        /// <summary>(Future story) New magazine spawned in the player's off-hand.</summary>
        MagSpawn = 3,

        /// <summary>(Future story) Magazine seated into the weapon receiver.</summary>
        MagInsertion = 4,

        /// <summary>(Future story) Slide racked to chamber the first round.</summary>
        SlideRack = 5,

        /// <summary>(Future story) Weapon grabbed from holster or world.</summary>
        WeaponGrab = 6,

        /// <summary>(Future story) Weapon returned to holster.</summary>
        WeaponHolster = 7,

        /// <summary>(Future story) Player took damage — pain/impact audio cue.</summary>
        DamageReceived = 8,

        /// <summary>(Future story) Player death event — terminal audio sting.</summary>
        PlayerDeath = 9,

        /// <summary>(Future story) Room transition started — door/warp sound begins.</summary>
        RoomTransitionStart = 10,

        /// <summary>(Future story) Room transition completed — arrival stinger.</summary>
        RoomTransitionEnd = 11,

        /// <summary>(Future story) Pause menu activated — UI whoosh / mute effect.</summary>
        PauseActivated = 12,

        /// <summary>(Future story) Snap-turn executed — brief orientation audio cue.</summary>
        SnapTurn = 13,

        /// <summary>(Future story) VR tracking lost — warning tone.</summary>
        TrackingLost = 14,

        /// <summary>
        /// Pitch-scaled 2D confirmation tone played on hitscan hit.
        /// Pitch is lerped from <c>pitchFloor</c> to <c>pitchCeiling</c> based on
        /// <c>final_damage / damage_cap</c> (see <see cref="FeedbackEventConfig.HitConfirmPitchFloor"/>,
        /// <see cref="FeedbackEventConfig.HitConfirmPitchCeiling"/>, <see cref="FeedbackEventConfig.HitConfirmDamageCap"/>).
        /// </summary>
        HitConfirmation = 15,

        /// <summary>(Future story) Bullet passed close to an enemy without hitting.</summary>
        EnemyNearMiss = 16,

        /// <summary>(Future story) Currency or loot item picked up.</summary>
        CurrencyPickup = 17,

        /// <summary>(Future story) VR tracking restored after a loss.</summary>
        TrackingRestored = 18,

        /// <summary>An enemy was destroyed. Posted by EnemyDeathRewards on Damageable.onDestroyed.</summary>
        EnemyDeath,
    }

    // =========================================================================
    // FeedbackHand — which controller drove the event
    // =========================================================================

    /// <summary>
    /// Identifies the VR hand associated with the feedback event.
    /// Used by the service for future haptic routing if needed.
    /// Not used for clip selection or positioning in MVP.
    /// </summary>
    public enum FeedbackHand
    {
        /// <summary>No hand association (e.g., hit-confirmation audio at a world position).</summary>
        None = 0,

        /// <summary>Event originated from the left controller.</summary>
        Left = 1,

        /// <summary>Event originated from the right controller.</summary>
        Right = 2,

        /// <summary>Event involves both controllers simultaneously (e.g., two-handed reload).</summary>
        Both = 3,
    }

    // =========================================================================
    // FeedbackVFXData — optional VFX payload (reserved for S1-007)
    // =========================================================================

    /// <summary>
    /// Optional VFX payload carried inside <see cref="FeedbackEventData"/>.
    /// Reserved for S1-007 (muzzle flash, hit spark). Not read by the Audio
    /// Feedback Service in MVP — a VFX system will consume it when implemented.
    ///
    /// <para>Inlined here to keep file count low. Move to its own file if the
    /// VFX payload grows complex.</para>
    /// </summary>
    public readonly struct FeedbackVFXData
    {
        /// <summary>Optional prefab key for the VFX to spawn. Null = no VFX request.</summary>
        public readonly string PrefabKey;

        /// <summary>World-space position to spawn the VFX at.</summary>
        public readonly Vector3 SpawnPosition;

        /// <summary>World-space rotation to orient the VFX.</summary>
        public readonly Quaternion SpawnRotation;

        /// <summary>Constructs a fully populated VFX data payload.</summary>
        public FeedbackVFXData(string prefabKey, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            PrefabKey      = prefabKey;
            SpawnPosition  = spawnPosition;
            SpawnRotation  = spawnRotation;
        }

        /// <summary>Returns <c>true</c> when no VFX has been requested (PrefabKey is null or empty).</summary>
        public bool IsEmpty => string.IsNullOrEmpty(PrefabKey);
    }

    // =========================================================================
    // FeedbackEventData — the full event struct posted to the service
    // =========================================================================

    /// <summary>
    /// Immutable data bag posted to <see cref="IAudioFeedbackService.PostFeedbackEvent"/>.
    /// All fields are value types (no heap allocation per event post).
    ///
    /// <para>Callers set only the fields relevant to their event type:
    /// <list type="bullet">
    ///   <item><c>WeaponFire</c>: set <c>EventType</c>, <c>Position</c> (muzzle), <c>Magnitude</c> (base dmg), <c>Hand</c></item>
    ///   <item><c>HitConfirmation</c>: set <c>EventType</c>, <c>Position</c> (hit point), <c>Magnitude</c> (final damage), <c>Hand = None</c></item>
    ///   <item><c>WeaponDryFire</c>: set <c>EventType</c>, <c>Hand</c>; Position and Magnitude are ignored</item>
    /// </list>
    /// </para>
    /// </summary>
    public readonly struct FeedbackEventData
    {
        /// <summary>Which feedback event to play. Drives config lookup.</summary>
        public readonly FeedbackEvent EventType;

        /// <summary>
        /// World-space position for 3D audio placement. Ignored when the matching
        /// config entry has <c>SpatialBlend == 0</c> (2D events).
        /// </summary>
        public readonly Vector3 Position;

        /// <summary>
        /// Numeric magnitude for this event. For <c>HitConfirmation</c>, this is
        /// <c>final_damage</c> used in pitch scaling. For <c>WeaponFire</c>, this is
        /// unused in MVP but carried for future volume scaling.
        /// </summary>
        public readonly float Magnitude;

        /// <summary>VR hand that triggered this event. Used for future haptic routing.</summary>
        public readonly FeedbackHand Hand;

        /// <summary>Optional VFX payload. Ignored by the Audio Feedback Service — reserved for S1-007.</summary>
        public readonly FeedbackVFXData VFX;

        /// <summary>Constructs a fully populated event descriptor.</summary>
        public FeedbackEventData(
            FeedbackEvent eventType,
            Vector3       position,
            float         magnitude,
            FeedbackHand  hand,
            FeedbackVFXData vfx = default)
        {
            EventType = eventType;
            Position  = position;
            Magnitude = magnitude;
            Hand      = hand;
            VFX       = vfx;
        }
    }
}
