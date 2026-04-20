namespace JerryScripts.Foundation.Damage
{
    /// <summary>
    /// Implemented by any game object that can receive damage from the combat system.
    /// Examples: enemy health components, destructible props, the player's hitbox relay.
    /// </summary>
    /// <remarks>
    /// Source: architecture.md §4.1 — IHittable contract (verbatim).
    /// GDD: damage-system.md §Interactions — "collider.GetComponent&lt;IHittable&gt;().TakeDamage(DamageEvent)".
    /// <para>
    /// The <c>in</c> modifier passes the readonly struct by reference without copying,
    /// matching the VR performance budget (no allocation, no boxing).
    /// </para>
    /// Implementors must handle the event on the main thread. The ProjectileSystem calls
    /// this synchronously inside <c>OnTriggerEnter</c> or hitscan resolution — never deferred.
    /// </remarks>
    public interface IHittable
    {
        /// <summary>
        /// Called by the Projectile System when a hitscan or physics projectile confirms a hit.
        /// The implementor is responsible for applying <see cref="DamageEvent.FinalDamage"/>
        /// to its own health state. Overkill (damage exceeds remaining health) is silently
        /// absorbed; the Damage System does not need to know current health.
        /// </summary>
        /// <param name="dmg">
        /// The fully resolved damage event. Passed by read-only reference — do not store a
        /// reference; copy any fields you need to retain.
        /// </param>
        void TakeDamage(in DamageEvent dmg);
    }
}
