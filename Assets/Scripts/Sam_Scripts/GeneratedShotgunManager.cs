using BNG;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RaycastWeapon))]
[RequireComponent(typeof(ShotgunPelletBurst))]
public sealed class GeneratedShotgunManager : MonoBehaviour
{
    [Header("Tier")]
    public GeneratedWeaponManager.WeaponRarityTier rarityTier = GeneratedWeaponManager.WeaponRarityTier.Common;

    [Header("References")]
    [SerializeField] private RaycastWeapon raycastWeapon;
    [SerializeField] private ShotgunPelletBurst pelletBurst;
    [SerializeField] private Grabbable recoilGrabbable;

    [Header("Generated Result")]
    public float projectileVelocityScale = 1f;
    public float weaponDamageScale = 1f;
    public int magazineSize = 4;
    public float recoilIntensityScale = 1f;

    public int MaxRarityRoll => Mathf.Clamp((int)rarityTier, 0, 2);

    private void Start()
    {
        ApplyTier(rarityTier);
    }

    public void ApplyTier(GeneratedWeaponManager.WeaponRarityTier tier)
    {
        rarityTier = tier;
        ResolveReferences();

        ShotgunTierStats stats = GetStats(tier);
        projectileVelocityScale = stats.ProjectileVelocityScale;
        weaponDamageScale = stats.DamageScale;
        magazineSize = stats.Capacity;
        recoilIntensityScale = stats.RecoilScale;

        if (pelletBurst != null)
        {
            pelletBurst.Configure(stats.PelletCount, stats.PelletDamage, stats.ShotForce, stats.SpreadAngle);
        }

        if (raycastWeapon != null)
        {
            // The VRIF weapon owns ammo, pump, sounds, haptics, muzzle flash, and recoil.
            // Damage comes from ShotgunPelletBurst so there is no extra center ray hit.
            raycastWeapon.Damage = 0f;
            raycastWeapon.MaxRange = 0.01f;
            raycastWeapon.AlwaysFireProjectile = false;
            raycastWeapon.FireProjectileInSlowMo = false;
            raycastWeapon.ShotForce = stats.ShotForce;
            raycastWeapon.FiringMethod = FiringType.Semi;
            raycastWeapon.ReloadMethod = ReloadType.InternalAmmo;
            raycastWeapon.AutoChamberRounds = true;
            raycastWeapon.MustChamberRounds = false;
            raycastWeapon.MaxInternalAmmo = stats.Capacity;
            raycastWeapon.InternalAmmo = stats.Capacity;
            raycastWeapon.RecoilForce *= stats.RecoilScale;
            raycastWeapon.RecoilForceTwoHanded *= stats.RecoilScale;
            raycastWeapon.RotationalRecoilForce *= stats.RecoilScale;
            raycastWeapon.RotationalRecoilForceTwoHanded *= stats.RecoilScale;
            raycastWeapon.RecoilDuration *= stats.RecoilScale;
        }

        if (recoilGrabbable != null)
        {
            recoilGrabbable.CollisionSlerp = Mathf.Max(100f, recoilGrabbable.CollisionSlerp / Mathf.Max(0.01f, stats.RecoilScale));
        }

        DisableSlideInteraction();
    }

    private void ResolveReferences()
    {
        if (raycastWeapon == null)
        {
            raycastWeapon = GetComponent<RaycastWeapon>();
        }

        if (pelletBurst == null)
        {
            pelletBurst = GetComponent<ShotgunPelletBurst>();
        }

        if (recoilGrabbable == null)
        {
            recoilGrabbable = GetComponent<Grabbable>();
        }
    }

    private void DisableSlideInteraction()
    {
        foreach (WeaponSlide slide in GetComponentsInChildren<WeaponSlide>(true))
        {
            slide.enabled = false;
        }
    }

    private static ShotgunTierStats GetStats(GeneratedWeaponManager.WeaponRarityTier tier)
    {
        switch (tier)
        {
            case GeneratedWeaponManager.WeaponRarityTier.Rare:
                return new ShotgunTierStats(8, 18f, 42f, 6, 15f, 1.25f, 1.15f, 1.05f);
            case GeneratedWeaponManager.WeaponRarityTier.Epic:
                return new ShotgunTierStats(10, 22f, 52f, 8, 12f, 1.55f, 1.35f, 1.15f);
            case GeneratedWeaponManager.WeaponRarityTier.Common:
            default:
                return new ShotgunTierStats(6, 15f, 32f, 4, 18f, 1f, 1f, 1f);
        }
    }

    private readonly struct ShotgunTierStats
    {
        public readonly int PelletCount;
        public readonly float PelletDamage;
        public readonly float ShotForce;
        public readonly int Capacity;
        public readonly float SpreadAngle;
        public readonly float DamageScale;
        public readonly float ProjectileVelocityScale;
        public readonly float RecoilScale;

        public ShotgunTierStats(
            int pelletCount,
            float pelletDamage,
            float shotForce,
            int capacity,
            float spreadAngle,
            float damageScale,
            float projectileVelocityScale,
            float recoilScale)
        {
            PelletCount = pelletCount;
            PelletDamage = pelletDamage;
            ShotForce = shotForce;
            Capacity = capacity;
            SpreadAngle = spreadAngle;
            DamageScale = damageScale;
            ProjectileVelocityScale = projectileVelocityScale;
            RecoilScale = recoilScale;
        }
    }
}
