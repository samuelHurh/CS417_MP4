using UnityEngine;

namespace JerryScripts.Foundation.Damage
{
    /// <summary>
    /// Stateless resolver that converts raw damage parameters into a resolved
    /// <see cref="DamageEvent"/>. Implementors must be pure functions: identical
    /// inputs always produce identical output with no observable side effects
    /// (aside from error-path <c>Debug.Log</c> calls on bad inputs).
    /// </summary>
    /// <remarks>
    /// Source: architecture.md §4.1 — IDamageResolver contract (verbatim).
    /// GDD: damage-system.md §Formulas.
    /// This interface lives in Foundation; all callers import downward from Core or Feature.
    /// </remarks>
    public interface IDamageResolver
    {
        /// <summary>
        /// Applies the player weapon damage formula and returns a resolved <see cref="DamageEvent"/>.
        /// </summary>
        /// <remarks>
        /// Formula: <c>final_damage = clamp(baseDamage * rarityMultiplier, 1.0, playerDamageCap)</c>.
        /// <para>Edge cases enforced by the resolver:</para>
        /// <list type="bullet">
        ///   <item><description>NaN or Infinity on either numeric input → floor + <c>Debug.LogError</c>.</description></item>
        ///   <item><description><paramref name="baseDamage"/> or <paramref name="rarityMultiplier"/> ≤ 0 → floor + <c>Debug.LogWarning</c>.</description></item>
        ///   <item><description>Result always clamped to [floor, playerDamageCap].</description></item>
        /// </list>
        /// </remarks>
        /// <param name="baseDamage">Raw damage stat from the weapon part (GDD range: 5–150).</param>
        /// <param name="rarityMultiplier">Rarity tier scalar (1.0, 1.3, 1.7, or 2.2).</param>
        /// <param name="hitPos">World-space contact point; passed through to the event unchanged.</param>
        /// <param name="sourceId">Weapon part ID; passed through to the event unchanged.</param>
        /// <returns>A fully resolved <see cref="DamageEvent"/> with <see cref="DamageEvent.IsPlayerSource"/> = <c>true</c>.</returns>
        DamageEvent ResolvePlayerDamage(float baseDamage, float rarityMultiplier,
                                        Vector3 hitPos, string sourceId);

        /// <summary>
        /// Applies the enemy attack damage formula and returns a resolved <see cref="DamageEvent"/>.
        /// </summary>
        /// <remarks>
        /// Formula: <c>final_damage = max(enemyBaseDamage * enemyDamageScalar, enemyDamageFloor)</c>.
        /// <para>Edge cases enforced by the resolver:</para>
        /// <list type="bullet">
        ///   <item><description>NaN or Infinity on <paramref name="enemyBaseDamage"/> → floor + <c>Debug.LogError</c>.</description></item>
        ///   <item><description>Result always at least <c>enemyDamageFloor</c> (default 1.0).</description></item>
        /// </list>
        /// </remarks>
        /// <param name="enemyBaseDamage">Per-enemy-type flat damage (GDD range: 5–60).</param>
        /// <param name="hitPos">World-space contact point; passed through to the event unchanged.</param>
        /// <param name="enemyId">Enemy type ID; passed through to the event unchanged.</param>
        /// <returns>A fully resolved <see cref="DamageEvent"/> with <see cref="DamageEvent.IsPlayerSource"/> = <c>false</c>.</returns>
        DamageEvent ResolveEnemyDamage(float enemyBaseDamage, Vector3 hitPos, string enemyId);
    }
}
