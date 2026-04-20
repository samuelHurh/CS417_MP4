using UnityEngine;
using JerryScripts.Feature.WeaponHandling;

namespace JerryScripts.Core.Projectile
{
    /// <summary>
    /// Hitscan projectile service consumed by <see cref="WeaponInstance"/>.
    /// </summary>
    /// <remarks>
    /// Scope: Jerry's player-side shooting only. Enemy projectile spawning is owned
    /// by Sam's Enemy System and is intentionally not on this interface.
    ///
    /// Layer: Core — may consume Foundation interfaces; must never reference Feature or
    /// Presentation types except <see cref="WeaponData"/> which is a pure data SO.
    ///
    /// <para><b>Performance contract:</b> <see cref="FireHitscan"/> allocates zero managed
    /// memory on the hot path (uses <c>Physics.Raycast</c> with out-parameter).</para>
    /// </remarks>
    public interface IProjectileService
    {
        /// <summary>
        /// Fires a hitscan ray from <paramref name="muzzle"/> along its forward vector.
        /// Resolves damage synchronously in the calling frame. Zero GC allocations.
        /// </summary>
        /// <param name="muzzle">
        /// Muzzle transform. Must remain valid for the duration of this synchronous call.
        /// Position and forward direction are sampled at the moment of the call —
        /// capture before any recoil is applied (GDD Rule 15).
        /// </param>
        /// <param name="weaponData">
        /// Weapon stat source. Must not be null. <c>BaseDamage &gt; 0</c> and
        /// <c>MaxRange &gt; 0</c> are caller guarantees.
        /// </param>
        /// <returns>
        /// <c>true</c> if the ray hit an object on the <c>EnemyHitbox</c> layer within
        /// <see cref="WeaponData.MaxRange"/>; <c>false</c> on a miss (no event emitted).
        /// </returns>
        bool FireHitscan(Transform muzzle, WeaponData weaponData);
    }
}
