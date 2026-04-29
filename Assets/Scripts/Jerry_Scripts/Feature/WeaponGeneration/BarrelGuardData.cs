using UnityEngine;

namespace JerryScripts.Feature.WeaponGeneration
{
    /// <summary>
    /// ScriptableObject that maps a single barrel-guard mesh prefab to its
    /// visual alignment offset on the universal lower receiver.
    ///
    /// <para>One asset per pool entry (Barrel-Guard-K through O for alpha).
    /// All five live in the shared rarity-agnostic
    /// <see cref="WeaponGenerationConfig.BarrelGuardPool"/> — mesh selection
    /// is uniform and independent of rarity tier (weapon-generation.md Rule 12).</para>
    ///
    /// <para><b>No rarity field.</b> Rarity is communicated through the HUD
    /// readout (HUD-06), never through mesh material or prefab choice.</para>
    /// </summary>
    /// <remarks>
    /// S2-008. GDD: weapon-generation.md §Mesh selection rules 9–11.
    /// Create via: Assets > Create > JerryScripts > Barrel Guard Data
    /// </remarks>
    [CreateAssetMenu(
        fileName = "NewBarrelGuardData",
        menuName  = "JerryScripts/Barrel Guard Data",
        order     = 1)]
    public sealed class BarrelGuardData : ScriptableObject
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Mesh")]
        [Tooltip(
            "Prefab that will be instantiated as a child of the universal lower's " +
            "BarrelGuardMountPoint transform. Authored material is rendered as-is — " +
            "no rarity tinting per weapon-generation.md Rule 10.")]
        [SerializeField] private GameObject _barrelGuardPrefab;

        [Header("Mount Alignment")]
        [Tooltip(
            "Local position offset applied to the barrel-guard after parenting to " +
            "BarrelGuardMountPoint. Author once in editor to visually align this " +
            "barrel-guard with the universal lower receiver.")]
        [SerializeField] private Vector3 _localPositionOffset = Vector3.zero;

        [Tooltip(
            "Local rotation offset (Euler angles) applied to the barrel-guard after " +
            "parenting. Author once in editor. Usually zero if the FBX is exported " +
            "in the correct orientation.")]
        [SerializeField] private Vector3 _localRotationOffset = Vector3.zero;

        [Tooltip(
            "Local scale applied to the barrel-guard after parenting. Useful for " +
            "fine-tuning size when meshes from different FBXs don't quite match the " +
            "lower receiver. Default (1, 1, 1) preserves the prefab's authored scale.")]
        [SerializeField] private Vector3 _localScale = Vector3.one;

        // ===================================================================
        // Public read-only API
        // ===================================================================

        /// <summary>
        /// Prefab instantiated as a child of the lower receiver's
        /// <c>BarrelGuardMountPoint</c>. May be null — caller must null-check
        /// per weapon-generation.md §Edge Cases (skip mesh attachment, log warning).
        /// </summary>
        public GameObject BarrelGuardPrefab => _barrelGuardPrefab;

        /// <summary>
        /// Local position offset applied after parenting to
        /// <c>BarrelGuardMountPoint</c>.
        /// </summary>
        public Vector3 LocalPositionOffset => _localPositionOffset;

        /// <summary>
        /// Local rotation offset (Euler angles) applied after parenting to
        /// <c>BarrelGuardMountPoint</c>.
        /// </summary>
        public Vector3 LocalRotationOffset => _localRotationOffset;

        /// <summary>
        /// Local scale applied after parenting to <c>BarrelGuardMountPoint</c>.
        /// Default <see cref="Vector3.one"/> — set in the inspector to fine-tune
        /// per-mesh sizing when the FBX scales don't match.
        /// </summary>
        public Vector3 LocalScale => _localScale;
    }
}
