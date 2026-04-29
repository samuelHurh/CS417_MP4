using UnityEngine;
using JerryScripts.Feature.WeaponHandling;

namespace JerryScripts.Feature.WeaponGeneration
{
    /// <summary>
    /// Stateless factory that produces randomised <see cref="WeaponData"/> runtime
    /// ScriptableObject instances and assembles the physical weapon prefab hierarchy.
    ///
    /// <para><b>Three entry points</b> (weapon-generation.md Rules 1–3):</para>
    /// <list type="bullet">
    ///   <item><see cref="GenerateRandom(WeaponRarity,WeaponGenerationConfig)"/> —
    ///     rolls stats + selects a barrel-guard from the shared pool.</item>
    ///   <item><see cref="GenerateRollRarity(WeaponGenerationConfig)"/> —
    ///     rolls a rarity first using shop-table probabilities, then delegates.</item>
    ///   <item><see cref="GenerateInitial(WeaponGenerationConfig)"/> —
    ///     convenience wrapper for a Basic-rarity weapon at scene start.</item>
    /// </list>
    ///
    /// <para>Generated <see cref="WeaponData"/> is a runtime-only
    /// <c>ScriptableObject.CreateInstance</c> — never a saved project asset.
    /// It is GC'd when the weapon is destroyed (weapon-generation.md Rule 6).</para>
    ///
    /// <para><c>max_range</c> is deliberately NOT randomised. It stays at the
    /// WeaponData SO default (50 m) for all generated weapons
    /// (weapon-generation.md §Tuning Knobs).</para>
    /// </summary>
    /// <remarks>
    /// S2-009. GDD: weapon-generation.md §Detailed Design, §Formulas.
    /// Architecture: stateless — no MonoBehaviour, no scene dependency.
    /// </remarks>
    public static class WeaponGenerator
    {
        // ===================================================================
        // BarrelGuardMountPoint name constant
        // ===================================================================

        /// <summary>
        /// Name of the child transform on the lower receiver prefab that receives
        /// the barrel-guard child. Must match the prefab hierarchy.
        /// </summary>
        private const string BarrelGuardMountPointName = "BarrelGuardMountPoint";

        /// <summary>
        /// Name of the debug cube instantiated when the barrel-guard pool is empty
        /// (weapon-generation.md Rule 12).
        /// </summary>
        private const string DebugCubeName = "BarrelGuard_DEBUG_EmptyPool";

        // ===================================================================
        // Public entry points
        // ===================================================================

        /// <summary>
        /// Produces a runtime <see cref="WeaponData"/> with all six stats rolled
        /// within <paramref name="rarity"/>'s bands.
        /// Also instantiates the physical weapon hierarchy (lower + barrel-guard)
        /// and returns it via <paramref name="weaponGameObject"/>.
        /// </summary>
        /// <param name="rarity">Requested rarity tier.</param>
        /// <param name="config">Designer config asset. Must not be null.</param>
        /// <param name="weaponGameObject">
        ///   Out — the assembled weapon root GameObject (lower receiver with barrel-guard child),
        ///   or null if <see cref="WeaponGenerationConfig.LowerReceiverPrefab"/> is unassigned.
        /// </param>
        /// <returns>Runtime-only <see cref="WeaponData"/> instance. Never null.</returns>
        public static WeaponData GenerateRandom(
            WeaponRarity rarity,
            WeaponGenerationConfig config,
            out GameObject weaponGameObject)
        {
            WeaponData data = RollStats(rarity, config);
            weaponGameObject = AssembleWeaponPrefab(config, data);
            return data;
        }

        /// <summary>
        /// Stats-only overload. Returns a runtime <see cref="WeaponData"/> with all
        /// six stats rolled inside <paramref name="rarity"/>'s bands. Does NOT
        /// instantiate any GameObject — use <see cref="AttachBarrelGuardTo"/>
        /// to attach a rolled barrel-guard to an already-instantiated weapon
        /// (e.g. after <see cref="WeaponSpawner"/> instantiates its prefab).
        /// </summary>
        public static WeaponData GenerateRandom(
            WeaponRarity rarity,
            WeaponGenerationConfig config)
        {
            return RollStats(rarity, config);
        }

        /// <summary>
        /// Rolls a rarity using the shop-table probability distribution
        /// (weapon-generation.md §Rarity Roll Probability), then delegates to
        /// <see cref="GenerateRandom(WeaponRarity,WeaponGenerationConfig,out GameObject)"/>.
        /// </summary>
        public static WeaponData GenerateRollRarity(
            WeaponGenerationConfig config,
            out GameObject weaponGameObject)
        {
            WeaponRarity rarity = RollRarity();
            return GenerateRandom(rarity, config, out weaponGameObject);
        }

        /// <summary>
        /// Stats-only overload. Rolls a rarity using shop-table probabilities,
        /// then returns a runtime <see cref="WeaponData"/> with stats rolled inside
        /// that rarity's bands. Does NOT instantiate any GameObject.
        /// </summary>
        public static WeaponData GenerateRollRarity(WeaponGenerationConfig config)
        {
            return RollStats(RollRarity(), config);
        }

        /// <summary>
        /// Convenience wrapper: generates a Basic-rarity pistol for the initial
        /// scene spawn (weapon-generation.md Rule 3).
        /// </summary>
        public static WeaponData GenerateInitial(
            WeaponGenerationConfig config,
            out GameObject weaponGameObject)
        {
            return GenerateRandom(WeaponRarity.Basic, config, out weaponGameObject);
        }

        /// <summary>
        /// Stats-only overload. Returns a Basic-rarity runtime <see cref="WeaponData"/>
        /// for the initial scene spawn. Does NOT instantiate any GameObject.
        /// Pair with <see cref="AttachBarrelGuardTo"/> after instantiating the
        /// player's weapon prefab.
        /// </summary>
        public static WeaponData GenerateInitial(WeaponGenerationConfig config)
        {
            return RollStats(WeaponRarity.Basic, config);
        }

        // ===================================================================
        // Stat rolling
        // ===================================================================

        private static WeaponData RollStats(WeaponRarity rarity, WeaponGenerationConfig config)
        {
            WeaponGenerationConfig.RarityStatBand band = config.GetBand(rarity);

            // Validate band (weapon-generation.md §Edge Cases — min > max)
            ValidateBand(band, rarity);

            // Roll each stat independently (weapon-generation.md Rule 4)
            float baseDamage      = Random.Range(band.BaseDamageMin,      band.BaseDamageMax);
            float roundsPerMinute = Random.Range(band.RoundsPerMinuteMin,  band.RoundsPerMinuteMax);
            int   magCapacity     = Random.Range(band.MagCapacityMin,      band.MagCapacityMax + 1); // +1: inclusive max
            float recoilPitch     = Random.Range(band.RecoilPitchBaseMin,  band.RecoilPitchBaseMax);
            float recoilYaw       = Random.Range(band.RecoilYawSpreadMin,  band.RecoilYawSpreadMax);
            float bulletSpeed     = Random.Range(band.BulletSpeedMin,      band.BulletSpeedMax);

            // Defensive clamps against SO [Range]/[Min] attributes (Rule 5)
            baseDamage      = Mathf.Max(1f,   baseDamage);
            roundsPerMinute = Mathf.Clamp(roundsPerMinute, 60f, 900f);
            magCapacity     = Mathf.Clamp(magCapacity, 1, 30);
            recoilPitch     = Mathf.Clamp(recoilPitch, 1f, 15f);
            recoilYaw       = Mathf.Clamp(recoilYaw,   0f, 5f);
            bulletSpeed     = Mathf.Max(1f, bulletSpeed);

            // Weapon name — "{Rarity} Pistol" per Rule 7
            string weaponName = $"{rarity} Pistol";

            // Create runtime-only instance — never saved to disk (Rule 6)
            WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
            data.Initialize(
                weaponName,
                rarity,
                baseDamage,
                roundsPerMinute,
                magCapacity,
                recoilPitch,
                recoilYaw,
                bulletSpeed);

            return data;
        }

        // ===================================================================
        // Rarity probability table
        // ===================================================================

        private static WeaponRarity RollRarity()
        {
            float roll = Random.Range(0f, 1f);
            if      (roll < 0.50f) return WeaponRarity.Basic;
            else if (roll < 0.80f) return WeaponRarity.Rare;
            else if (roll < 0.95f) return WeaponRarity.Epic;
            else                   return WeaponRarity.Legendary;
        }

        // ===================================================================
        // Prefab assembly
        // ===================================================================

        /// <summary>
        /// Instantiates the lower receiver prefab, selects a barrel-guard from
        /// the pool, and parents it to the lower's <c>BarrelGuardMountPoint</c>
        /// child with the SO's offset/rotation applied (Rules 11–12).
        /// </summary>
        private static GameObject AssembleWeaponPrefab(
            WeaponGenerationConfig config,
            WeaponData data)
        {
            if (config.LowerReceiverPrefab == null)
            {
                Debug.LogWarning(
                    "[WeaponGenerator] LowerReceiverPrefab is not assigned in WeaponGenerationConfig. " +
                    "No weapon GameObject will be produced. Assign the prefab in the config asset.",
                    config);
                return null;
            }

            // Instantiate universal lower receiver
            GameObject weaponRoot = Object.Instantiate(config.LowerReceiverPrefab);
            weaponRoot.name = $"{data.Rarity}_Pistol_{weaponRoot.GetEntityId()}";

            // Find the barrel-guard mount point child transform
            Transform mountPoint = FindBarrelGuardMount(weaponRoot);

            // Select a barrel-guard from the shared pool (Rule 12 — rarity-agnostic, uniform)
            AttachBarrelGuard(config, mountPoint, weaponRoot);

            return weaponRoot;
        }

        /// <summary>
        /// Public entry point used by <see cref="WeaponSpawner"/> after it has
        /// instantiated a fully-equipped pistol prefab (interactable + lower-receiver
        /// mesh + <c>BarrelGuardMountPoint</c> child). Finds the mount point on the
        /// supplied <paramref name="weaponRoot"/> and parents a rolled barrel-guard
        /// to it (Rules 11–12). Falls back to the empty-pool magenta-cube path if
        /// the pool is empty.
        /// </summary>
        /// <param name="weaponRoot">
        ///   The instantiated weapon GameObject. Must contain a child Transform
        ///   named <c>BarrelGuardMountPoint</c>; otherwise the barrel-guard parents
        ///   to the root with a warning.
        /// </param>
        /// <param name="config">Designer config asset. Must not be null.</param>
        public static void AttachBarrelGuardTo(GameObject weaponRoot, WeaponGenerationConfig config)
        {
            if (weaponRoot == null)
            {
                Debug.LogError("[WeaponGenerator] AttachBarrelGuardTo: weaponRoot is null.");
                return;
            }
            if (config == null)
            {
                Debug.LogError("[WeaponGenerator] AttachBarrelGuardTo: config is null.", weaponRoot);
                return;
            }

            Transform mountPoint = FindBarrelGuardMount(weaponRoot);
            AttachBarrelGuard(config, mountPoint, weaponRoot);
        }

        private static Transform FindBarrelGuardMount(GameObject lower)
        {
            Transform mount = lower.transform.Find(BarrelGuardMountPointName);
            if (mount == null)
            {
                Debug.LogWarning(
                    $"[WeaponGenerator] '{BarrelGuardMountPointName}' child not found on lower receiver prefab. " +
                    "Barrel-guard will be parented to the root. Check the prefab hierarchy.",
                    lower);
                return lower.transform;
            }
            return mount;
        }

        private static void AttachBarrelGuard(
            WeaponGenerationConfig config,
            Transform mountPoint,
            GameObject weaponRoot)
        {
            BarrelGuardData[] pool = config.BarrelGuardPool;

            // Empty pool fallback — magenta cube + error (Rule 12)
            if (pool == null || pool.Length == 0)
            {
                Debug.LogError(
                    "[WeaponGenerator] BarrelGuardPool is empty in WeaponGenerationConfig. " +
                    "Spawning a magenta debug cube as the barrel guard. " +
                    "Populate the pool with BarrelGuardData assets before shipping.",
                    config);

                GameObject debugCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debugCube.name = DebugCubeName;
                debugCube.transform.SetParent(mountPoint, false);
                debugCube.transform.localScale = new Vector3(0.02f, 0.02f, 0.06f);
                var debugRenderer = debugCube.GetComponent<Renderer>();
                var debugMat = new Material(debugRenderer.sharedMaterial) { color = Color.magenta };
                debugRenderer.sharedMaterial = debugMat;
                return;
            }

            // Uniform random selection (Rule 12)
            int index = Random.Range(0, pool.Length);
            BarrelGuardData selected = pool[index];

            // Null prefab — skip mesh attachment, log warning (weapon-generation.md §Edge Cases)
            if (selected == null || selected.BarrelGuardPrefab == null)
            {
                Debug.LogWarning(
                    $"[WeaponGenerator] BarrelGuardData at pool index {index} has a null barrelGuardPrefab. " +
                    "Skipping mesh attachment — lower-only weapon will still function.",
                    config);
                return;
            }

            GameObject barrelGuard = Object.Instantiate(selected.BarrelGuardPrefab, mountPoint, false);
            barrelGuard.transform.localPosition = selected.LocalPositionOffset;
            barrelGuard.transform.localRotation = Quaternion.Euler(selected.LocalRotationOffset);

            // Defensive: a zero scale would make the mesh invisible. Most likely
            // cause is an asset authored before _localScale was added (Unity deserializes
            // missing fields to default(T) = Vector3.zero rather than the C# initializer).
            // Treat zero as "no scale override" and use Vector3.one.
            Vector3 scale = selected.LocalScale;
            barrelGuard.transform.localScale = (scale == Vector3.zero) ? Vector3.one : scale;
        }

        // ===================================================================
        // Validation helpers
        // ===================================================================

        private static void ValidateBand(WeaponGenerationConfig.RarityStatBand band, WeaponRarity rarity)
        {
            ValidateFloatBand(band.BaseDamageMin,      band.BaseDamageMax,      rarity, "base_damage");
            ValidateFloatBand(band.RoundsPerMinuteMin, band.RoundsPerMinuteMax, rarity, "rounds_per_minute");
            ValidateIntBand  (band.MagCapacityMin,     band.MagCapacityMax,     rarity, "mag_capacity");
            ValidateFloatBand(band.RecoilPitchBaseMin, band.RecoilPitchBaseMax, rarity, "recoil_pitch_base");
            ValidateFloatBand(band.RecoilYawSpreadMin, band.RecoilYawSpreadMax, rarity, "recoil_yaw_spread");
            ValidateFloatBand(band.BulletSpeedMin,     band.BulletSpeedMax,     rarity, "bullet_speed");
        }

        private static void ValidateFloatBand(
            float min, float max, WeaponRarity rarity, string statName)
        {
            if (min > max)
            {
                Debug.LogError(
                    $"[WeaponGenerator] '{rarity}' band for '{statName}' has min ({min}) > max ({max}). " +
                    "Config error — generation will clamp to min. Fix WeaponGenerationConfig.");
            }
        }

        private static void ValidateIntBand(
            int min, int max, WeaponRarity rarity, string statName)
        {
            if (min > max)
            {
                Debug.LogError(
                    $"[WeaponGenerator] '{rarity}' band for '{statName}' has min ({min}) > max ({max}). " +
                    "Config error — generation will clamp to min. Fix WeaponGenerationConfig.");
            }
        }
    }
}
