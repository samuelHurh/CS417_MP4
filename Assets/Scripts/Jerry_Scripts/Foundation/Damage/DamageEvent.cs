using UnityEngine;

namespace JerryScripts.Foundation.Damage
{
    /// <summary>
    /// Immutable data bag produced by <see cref="IDamageResolver"/> and consumed by
    /// <see cref="IHittable.TakeDamage"/> and downstream systems (audio, VFX, HUD).
    /// The struct carries no behavior — it is a pure data transfer object.
    /// </summary>
    /// <remarks>
    /// Source: architecture.md §4.1 — DamageEvent contract (verbatim).
    /// GDD: damage-system.md §Detailed Design Core Rule 3.
    /// </remarks>
    public readonly struct DamageEvent
    {
        /// <summary>
        /// Resolved damage after all multipliers, floors, and caps have been applied.
        /// Guaranteed to be in the range [<see cref="RarityMultiplierTable.EnemyDamageFloor"/>,
        /// <see cref="RarityMultiplierTable.PlayerDamageCap"/>].
        /// </summary>
        public readonly float FinalDamage;

        /// <summary>
        /// Weapon part ID or enemy type ID. Used by logging, VFX routing, and audio
        /// feedback to identify the source of the damage. Never null.
        /// </summary>
        public readonly string SourceId;

        /// <summary>
        /// <c>true</c> when the player fired the shot; <c>false</c> when an enemy attacked.
        /// Downstream systems (PlayerRig, AudioFeedback) use this to route the event correctly.
        /// </summary>
        public readonly bool IsPlayerSource;

        /// <summary>
        /// World-space contact point. Passed through verbatim from the caller.
        /// Used by the Audio/Feedback System for VFX placement and 3D audio positioning.
        /// Has no effect on damage math in MVP.
        /// </summary>
        public readonly Vector3 HitPosition;

        /// <summary>
        /// Constructs a fully initialised <see cref="DamageEvent"/>.
        /// All fields are set at construction time and are thereafter immutable.
        /// </summary>
        /// <param name="finalDamage">Resolved damage value, already clamped by the resolver.</param>
        /// <param name="sourceId">Weapon part ID or enemy type ID. Must not be null.</param>
        /// <param name="isPlayerSource"><c>true</c> for player-fired; <c>false</c> for enemy attack.</param>
        /// <param name="hitPosition">World-space contact point for VFX/audio placement.</param>
        public DamageEvent(float finalDamage, string sourceId, bool isPlayerSource, Vector3 hitPosition)
        {
            FinalDamage    = finalDamage;
            SourceId       = sourceId;
            IsPlayerSource = isPlayerSource;
            HitPosition    = hitPosition;
        }
    }
}
