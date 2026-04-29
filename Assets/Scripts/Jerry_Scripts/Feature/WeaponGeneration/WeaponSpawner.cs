using UnityEngine;
using JerryScripts.Foundation;
using JerryScripts.Feature.WeaponHandling;

namespace JerryScripts.Feature.WeaponGeneration
{
    /// <summary>
    /// Scene-level MonoBehaviour that calls
    /// <see cref="WeaponGenerator.GenerateInitial"/> exactly once on
    /// <see cref="Awake"/> and parents the result to
    /// <c>IMountPointProvider.RightHipHolster</c> in Holstered state
    /// (weapon-generation.md Rules 13–15).
    ///
    /// <para>Attach this component to the <c>_Systems</c> GameObject alongside
    /// the other system components. Wire <see cref="_config"/> in the Inspector
    /// to the project's <c>WeaponGenerationConfig.asset</c>.</para>
    ///
    /// <para><b>Double-spawn guard</b>: if <see cref="Awake"/> is called more
    /// than once in the same scene (defensive), the second call is a no-op with
    /// a warning (weapon-generation.md §Edge Cases).</para>
    /// </summary>
    /// <remarks>
    /// S2-009. GDD: weapon-generation.md §Initial spawn rules 13–15.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WeaponSpawner : MonoBehaviour
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Config")]
        [Tooltip("WeaponGenerationConfig asset. Required — without it no weapon spawns and an error is logged.")]
        [SerializeField] private WeaponGenerationConfig _config;

        [Header("Weapon Instance Prefab")]
        [Tooltip(
            "Prefab that carries the WeaponInstance MonoBehaviour (trigger, mag well, etc.). " +
            "The generated WeaponData SO is injected into its WeaponData field at spawn time. " +
            "If null, the spawner produces a data-only WeaponData with no interactable in the scene.")]
        [SerializeField] private GameObject _weaponInstancePrefab;

        // ===================================================================
        // Private runtime state
        // ===================================================================

        private bool _hasSpawned;

        // ===================================================================
        // Unity lifecycle
        // ===================================================================

        private void Awake()
        {
            if (_hasSpawned)
            {
                Debug.LogWarning(
                    "[WeaponSpawner] GenerateInitial() called more than once in this scene. " +
                    "Second call ignored — first weapon stays at MountPoint_HipR. " +
                    "weapon-generation.md §Edge Cases.",
                    this);
                return;
            }

            if (_config == null)
            {
                Debug.LogError(
                    "[WeaponSpawner] WeaponGenerationConfig is not assigned. " +
                    "No initial weapon will spawn. Player starts unarmed. " +
                    "weapon-generation.md §Edge Cases.",
                    this);
                return;
            }

            _hasSpawned = true;
            SpawnInitialWeapon();
        }

        // ===================================================================
        // Spawn
        // ===================================================================

        private void SpawnInitialWeapon()
        {
            // Resolve mount point from scene (no inspector wiring — consistent
            // with WeaponInstance auto-resolution pattern)
            var rig = FindAnyObjectByType<PlayerRig>();
            IMountPointProvider mountProvider = rig;

            Transform hipMount = mountProvider?.RightHipHolster;

            // Generate stats + assembled weapon mesh hierarchy
            WeaponData generatedData = WeaponGenerator.GenerateInitial(
                _config,
                out GameObject weaponMeshRoot);

            // Instantiate the interactable prefab and inject the generated data
            if (_weaponInstancePrefab != null)
            {
                GameObject instanceGO = Instantiate(_weaponInstancePrefab);
                instanceGO.name = $"Weapon_{generatedData.Rarity}_Initial";

                // Inject generated WeaponData via the internal seam
                var weaponInstance = instanceGO.GetComponent<Feature.WeaponHandling.WeaponInstance>();
                if (weaponInstance != null)
                {
                    weaponInstance.InjectGeneratedData(generatedData);
                }
                else
                {
                    Debug.LogWarning(
                        "[WeaponSpawner] _weaponInstancePrefab has no WeaponInstance component. " +
                        "Generated WeaponData will not be applied to the interactable.",
                        this);
                }

                // Parent the mesh hierarchy under the interactable root
                if (weaponMeshRoot != null)
                {
                    weaponMeshRoot.transform.SetParent(instanceGO.transform, false);
                    weaponMeshRoot.transform.localPosition = Vector3.zero;
                    weaponMeshRoot.transform.localRotation = Quaternion.identity;
                }

                // Parent to hip holster mount (Holstered state)
                if (hipMount != null)
                {
                    instanceGO.transform.SetParent(hipMount, false);
                    instanceGO.transform.localPosition = Vector3.zero;
                    instanceGO.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    Debug.LogWarning(
                        "[WeaponSpawner] MountPoint_HipR not found. " +
                        "Initial weapon placed at world origin.",
                        this);
                    instanceGO.transform.position = Vector3.zero;
                }
            }
            else
            {
                // No interactable prefab — place the raw mesh (editor/test usage)
                if (weaponMeshRoot != null)
                {
                    if (hipMount != null)
                    {
                        weaponMeshRoot.transform.SetParent(hipMount, false);
                        weaponMeshRoot.transform.localPosition = Vector3.zero;
                        weaponMeshRoot.transform.localRotation = Quaternion.identity;
                    }
                }

                Debug.LogWarning(
                    "[WeaponSpawner] _weaponInstancePrefab is null — no interactable weapon " +
                    "was created. Assign a prefab with a WeaponInstance component.",
                    this);
            }
        }

        // ===================================================================
        // Test seam
        // ===================================================================

        /// <summary>
        /// Allows unit tests to inject a config without scene wiring.
        /// </summary>
        internal void InjectConfig(WeaponGenerationConfig config) => _config = config;

        /// <summary>True once <see cref="SpawnInitialWeapon"/> has run.</summary>
        internal bool HasSpawned => _hasSpawned;
    }
}
