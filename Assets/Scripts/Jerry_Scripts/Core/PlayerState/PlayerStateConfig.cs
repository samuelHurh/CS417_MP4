using UnityEngine;

namespace JerryScripts.Core.PlayerState
{
    /// <summary>
    /// ScriptableObject containing all designer-tunable parameters for the
    /// <see cref="PlayerStateManager"/>. Create one asset via the Assets menu
    /// and assign it in the Inspector.
    ///
    /// <para>Keeping these values out of the MonoBehaviour allows balance
    /// changes without code recompilation and supports multiple presets
    /// (normal/hard/easy) simply by swapping the asset reference.</para>
    /// </summary>
    /// <remarks>S2-002. GDD: player-state-management.md §Tuning Knobs.</remarks>
    [CreateAssetMenu(
        fileName = "PlayerStateConfig",
        menuName  = "CS417/Foundation/Player State Config",
        order     = 1)]
    public sealed class PlayerStateConfig : ScriptableObject
    {
        // -------------------------------------------------------------------
        // Health
        // -------------------------------------------------------------------

        [Header("Health")]
        [Tooltip("Maximum health points for one run. Health initializes to this value on start and restart.")]
        [Min(1f)]
        [SerializeField] private float _maxHealth = 100f;

        /// <summary>
        /// Maximum health points per run. Minimum 1 (enforced by <c>[Min]</c> attribute).
        /// Default: 100.
        /// </summary>
        public float MaxHealth => _maxHealth;

        // -------------------------------------------------------------------
        // Currency
        // -------------------------------------------------------------------

        [Header("Currency")]
        [Tooltip("Starting currency balance at run start (and after restart).")]
        [Min(0)]
        [SerializeField] private int _startingCurrency = 0;

        /// <summary>Currency the player begins each run with. Default: 0.</summary>
        public int StartingCurrency => _startingCurrency;
    }
}
