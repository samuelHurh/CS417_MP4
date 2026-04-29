using UnityEngine;
using JerryScripts.Feature.WeaponHandling;

namespace JerryScripts.Feature.WeaponGeneration
{
    /// <summary>
    /// Scene-level MonoBehaviour that calls
    /// <see cref="WeaponGenerator.GenerateInitial"/> exactly once on
    /// <see cref="Awake"/>, instantiates the fully-equipped pistol prefab
    /// (<see cref="_weaponInstancePrefab"/> — typically <c>Pistol_Basic</c>),
    /// attaches a rolled barrel-guard to its <c>BarrelGuardMountPoint</c> child,
    /// and parents the result to a designer-placed <see cref="_spawnPoint"/>
    /// Transform in the environment (e.g. resting on a desk).
    ///
    /// <para>Attach this component to the <c>_Systems</c> GameObject alongside
    /// the other system components. Wire <see cref="_config"/>,
    /// <see cref="_weaponInstancePrefab"/>, and <see cref="_spawnPoint"/> in the Inspector.</para>
    ///
    /// <para>The weapon is left in <b>Idle</b> state (kinematic Rigidbody on the
    /// prefab) so it sits still on the spawn surface. The player walks up and
    /// grabs it via the prefab's <c>XRGrabInteractable</c>.</para>
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

        [Header("Spawn Point")]
        [Tooltip(
            "World-space Transform where the initial weapon spawns. Place an empty " +
            "GameObject in the scene at the spot where the weapon should rest (e.g. " +
            "on a desk or table) and drag it here. The spawned weapon is parented to " +
            "this Transform with localPosition=0 and localRotation=identity, so position " +
            "and rotation are entirely controlled by this Transform's world pose. " +
            "If null, the weapon spawns at world origin with a warning.")]
        [SerializeField] private Transform _spawnPoint;

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
                    "Second call ignored — first weapon stays at the assigned spawn point. " +
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
            // Stats-only generation — no GameObject side effect.
            // We instantiate the fully-equipped prefab below and attach the rolled
            // barrel-guard to its existing BarrelGuardMountPoint child.
            WeaponData generatedData = WeaponGenerator.GenerateInitial(_config);

            if (_weaponInstancePrefab == null)
            {
                Debug.LogWarning(
                    "[WeaponSpawner] _weaponInstancePrefab is null — no interactable weapon " +
                    "was created. Assign a prefab with a WeaponInstance component (e.g. Pistol_Basic).",
                    this);
                return;
            }

            GameObject instanceGO = Instantiate(_weaponInstancePrefab);
            instanceGO.name = $"Weapon_{generatedData.Rarity}_Initial";

            // Force the spawned weapon to rest on the spawn point until grabbed.
            // Defense-in-depth: even if the prefab's Inspector has a non-kinematic
            // Rigidbody, the weapon shouldn't fall at scene start. We set isKinematic
            // only — leave useGravity at the prefab's authored value so the moment
            // WeaponInstance.EnterState(Dropped) flips isKinematic to false, gravity
            // takes over naturally and the released weapon falls as expected.
            var spawnRb = instanceGO.GetComponent<Rigidbody>();
            if (spawnRb != null)
            {
                spawnRb.isKinematic = true;
            }

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

            // Attach a rolled barrel-guard to the prefab's BarrelGuardMountPoint child.
            // The mesh selection is uniform-random and rarity-agnostic (Rule 12).
            WeaponGenerator.AttachBarrelGuardTo(instanceGO, _config);

            // Parent to the designer-placed spawn point
            ParentToSpawnPoint(instanceGO.transform);
        }

        /// <summary>
        /// Sets <paramref name="weapon"/>'s world pose to <see cref="_spawnPoint"/>'s
        /// current world pose, then leaves the weapon unparented. The weapon stays at
        /// that fixed world location regardless of what happens to the spawn-point
        /// Transform afterward (e.g. XR rig calibration moving a parent, runtime
        /// scripts repositioning the spawn-point hierarchy). The kinematic Rigidbody
        /// keeps it from falling; <see cref="WeaponInstance"/> takes over on grab.
        /// If the spawn point is unassigned, the weapon stays at world origin with a warning.
        /// </summary>
        private void ParentToSpawnPoint(Transform weapon)
        {
            Vector3    worldPos;
            Quaternion worldRot;

            if (_spawnPoint != null)
            {
                worldPos = _spawnPoint.position;
                worldRot = _spawnPoint.rotation;

                Debug.Log(
                    $"[WeaponSpawner] Spawning at _spawnPoint world pose: " +
                    $"position={worldPos}, rotation={worldRot.eulerAngles}. " +
                    $"(If position is (0, 0, 0) but you placed the spawn point elsewhere, " +
                    $"the spawn point is being moved by something else — check its parent chain " +
                    $"in the Hierarchy and any scripts on parent GameObjects.)",
                    this);
            }
            else
            {
                Debug.LogWarning(
                    "[WeaponSpawner] _spawnPoint is not assigned. Initial weapon placed at world origin. " +
                    "Drag a scene Transform into _spawnPoint to control where the weapon spawns.",
                    this);
                worldPos = Vector3.zero;
                worldRot = Quaternion.identity;
            }

            weapon.SetParent(null, true);
            weapon.position = worldPos;
            weapon.rotation = worldRot;

            // Sync the Rigidbody's internal physics state to match the Transform.
            // After Instantiate, the Rigidbody's cached position is world origin (where
            // the GameObject was created). Without this sync, physics can override the
            // Transform position on the first physics tick, snapping the weapon back to
            // the prefab's instantiate-time position.
            var rb = weapon.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = worldPos;
                rb.rotation = worldRot;
            }

            Debug.Log(
                $"[WeaponSpawner] After position set — weapon.transform.position={weapon.position}, " +
                $"Rigidbody.position={(rb != null ? rb.position.ToString() : "n/a")}. " +
                $"(If these don't match the spawn-point log above, something moved the weapon " +
                $"between the two log statements — share both lines for diagnosis.)",
                this);
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
