using UnityEngine;
using System.Collections.Generic;
using BNG;

public class GeneratedWeaponManager : MonoBehaviour
{

    public bool isBasic;
    public Material basicGripMaterial;
    public Material basicSlideMaterial;
    public bool isAuto;
    public Material autoMaterial;
    public enum WeaponRarityTier
    {
        Common,
        Rare,
        Epic
    }

    [System.Serializable]
    public struct GeneratedWeaponPackage
    {
        public int slideLength;
        public int gripSize;
        public int slideRarity;
        public int gripRarity;

        public GeneratedWeaponPackage(int slideLength, int gripSize, int slideRarity, int gripRarity)
        {
            this.slideLength = slideLength;
            this.gripSize = gripSize;
            this.slideRarity = slideRarity;
            this.gripRarity = gripRarity;
        }

        public override string ToString()
        {
            return $"SlideLength: {slideLength}, GripSize: {gripSize}, SlideRarity: {slideRarity}, GripRarity: {gripRarity}";
        }
    }

    private class WeaponAttribute
    {
        public int value;
        public readonly int maxValue;

        public WeaponAttribute(int maxValue)
        {
            value = 0;
            this.maxValue = maxValue;
        }

        public bool CanIncrease => value < maxValue;
    }

    [Header("Generation")]
    public WeaponRarityTier rarityTier = WeaponRarityTier.Common;

    public List<GameObject> Frames = new List<GameObject>();
    public List<GameObject> Slides = new List<GameObject>();

    public List<Material> slideMaterials = new List<Material>(); //hardcoded to 3 values in editor
    public List<Material> gripMaterials = new List<Material>(); //hardcoded to 2 values in editor

    [Header("Generated Result")]
    public GeneratedWeaponPackage generatedPackage;

    [Header("Future Stat Outputs")]
    public float projectileVelocityScale = 1f;
    public float weaponDamageScale = 1f;
    public int magazineSize = 15;
    public float recoilIntensityScale = 1f;
    public float linearRecoilIntensityScale = 1f;
    public float rotationalRecoilIntensityScale = 1f;
    public float recoilReturnTimeScale = 1f;

    [Header("Recoil Application")]
    [SerializeField] private RaycastWeapon raycastWeapon;
    [SerializeField] private Grabbable recoilGrabbable;
    [SerializeField] private Vector3 baseLinearRecoilForce = Vector3.zero;
    [SerializeField] private Vector3 baseLinearRecoilForceTwoHanded = Vector3.zero;
    [SerializeField] private Vector3 baseRotationalRecoilForce = new Vector3(-50f, 0f, 0f);
    [SerializeField] private Vector3 baseRotationalRecoilForceTwoHanded = Vector3.zero;
    [SerializeField] private float baseRecoilDuration = 0.5f;
    [SerializeField] private float baseRotationalReturnSpring = 500f;
    [SerializeField] private float baseRotationalReturnDamper = 1f;
    [SerializeField] private Vector2 linearRecoilIntensityScaleRange = new Vector2(1.35f, 0.75f);
    [SerializeField] private Vector2 rotationalRecoilIntensityScaleRange = new Vector2(1.35f, 0.75f);
    [SerializeField] private Vector2 recoilReturnTimeScaleRange = new Vector2(1.35f, 0.75f);

    [Header("Projectile Application")]
    [SerializeField] private float baseShotForce = 20f;
    [SerializeField] private Vector2 projectileVelocityScaleRange = new Vector2(0.85f, 1.35f);
    [SerializeField] private float baseWeaponDamage = 25f;
    [SerializeField] private Vector2 weaponDamageScaleRange = new Vector2(0.85f, 1.35f);

    public RGBMaterial potentialRGBColorSlide;
    public bool HasCompactGrip => Mathf.Clamp(generatedPackage.gripSize, 0, 1) == 0;
    public int CurrentMagazineCapacity => HasCompactGrip ? 9 : 15;

    //The normal slide and the non-compact grip are the origin transform.
    //Different slide lengths require moving the frame in comparison
    // the compact grip requires moving the frame up
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (isBasic)
        {
            generatedPackage = GenerateWeaponPackage(rarityTier);
        }else if (isAuto)
        {
            generatedPackage = GenerateWeaponPackage(rarityTier);
        } else
        {
            generatedPackage = GenerateWeaponPackage(rarityTier);
        }
        ApplyGeneratedPackage(generatedPackage);
        CacheGeneratedStats(generatedPackage);
        ApplyGeneratedStatsToWeapon();
    }

    private GeneratedWeaponPackage GenerateWeaponPackage(WeaponRarityTier tier)
    {
        if (isBasic)
        {
            return new GeneratedWeaponPackage(0, 0, 0, 0);
        } else if (isAuto)
        {
            return new GeneratedWeaponPackage(2,1,2,1);
        }
        int budget = RollBudget(tier);
        Debug.Log("Budget: " + budget);

        WeaponAttribute slideLength = new WeaponAttribute(2);
        WeaponAttribute gripSize = new WeaponAttribute(1);
        WeaponAttribute slideRarity = new WeaponAttribute(2);
        WeaponAttribute gripRarity = new WeaponAttribute(1);

        List<WeaponAttribute> attributes = new List<WeaponAttribute>
        {
            slideLength,
            gripSize,
            slideRarity,
            gripRarity
        };

        while (budget > 0)
        {
            List<WeaponAttribute> upgradeableAttributes = attributes.FindAll(attribute => attribute.CanIncrease);
            if (upgradeableAttributes.Count == 0)
            {
                break;
            }

            WeaponAttribute chosenAttribute = upgradeableAttributes[Random.Range(0, upgradeableAttributes.Count)];
            chosenAttribute.value++;
            budget--;
        }

        return new GeneratedWeaponPackage(slideLength.value, gripSize.value, slideRarity.value, gripRarity.value);
    }

    private int RollBudget(WeaponRarityTier tier)
    {
        switch (tier)
        {
            case WeaponRarityTier.Common:
                return Random.Range(1, 3);
            case WeaponRarityTier.Rare:
                return Random.Range(3, 5);
            case WeaponRarityTier.Epic:
                return Random.Range(5, 7);
            default:
                return 0;
        }
    }

    private void ApplyGeneratedPackage(GeneratedWeaponPackage package)
    {
        foreach (GameObject frame in Frames)
        {
            frame.SetActive(false);
        }

        foreach (GameObject slide in Slides)
        {
            slide.SetActive(false);
        }

        if (Slides.Count < 3 || Frames.Count < 6)
        {
            Debug.LogWarning("GeneratedWeaponManager needs 3 Slides and 6 Frames assigned.", this);
            return;
        }

        GameObject chosenSlide = GetSlideForPackage(package);
        GameObject chosenFrame = GetFrameForPackage(package);

        chosenFrame.SetActive(true);
        chosenSlide.SetActive(true);
        if (isBasic)
        {
            if (slideMaterials.Count > package.slideRarity)
            {
                chosenSlide.GetComponent<Renderer>().material = basicSlideMaterial;
            }

            if (gripMaterials.Count > package.gripRarity)
            {
                chosenFrame.GetComponent<Renderer>().material = basicGripMaterial;
            }
        } else if (isAuto)
        {
            if (slideMaterials.Count > package.slideRarity)
            {
                chosenSlide.GetComponent<Renderer>().material = autoMaterial;
            }

            if (gripMaterials.Count > package.gripRarity)
            {
                chosenFrame.GetComponent<Renderer>().material = autoMaterial;
            }
        } else
        {
            if (slideMaterials.Count > package.slideRarity)
            {
                chosenSlide.GetComponent<Renderer>().material = slideMaterials[package.slideRarity];
            }

            if (gripMaterials.Count > package.gripRarity)
            {
                chosenFrame.GetComponent<Renderer>().material = gripMaterials[package.gripRarity];
            }

            if (package.slideRarity == 2 && potentialRGBColorSlide != null)
            {
                potentialRGBColorSlide.ReceiveSetup(chosenSlide);
            }
        }
        

        Debug.Log($"Generated weapon package: {package}", this);
    }

    private GameObject GetSlideForPackage(GeneratedWeaponPackage package)
    {
        int slideListIndex = GetSlideListIndex(package.slideLength);
        return Slides[slideListIndex];
    }

    private GameObject GetFrameForPackage(GeneratedWeaponPackage package)
    {
        int slideListIndex = GetSlideListIndex(package.slideLength);
        int gripSize = Mathf.Clamp(package.gripSize, 0, 1);
        int frameIndex = slideListIndex + (1 - gripSize) * 3;
        return Frames[frameIndex];
    }

    private int GetSlideListIndex(int slideLength)
    {
        switch (Mathf.Clamp(slideLength, 0, 2))
        {
            case 0:
                return 2;
            case 1:
                return 1;
            case 2:
                return 0;
            default:
                return 1;
        }
    }

    private void CacheGeneratedStats(GeneratedWeaponPackage package)
    {
        projectileVelocityScale = 1f;
        weaponDamageScale = 1f;
        magazineSize = package.gripSize == 0 ? 9 : 15;
        projectileVelocityScale = Mathf.Lerp(
            projectileVelocityScaleRange.x,
            projectileVelocityScaleRange.y,
            GetProjectileQuality01(package));
        weaponDamageScale = Mathf.Lerp(
            weaponDamageScaleRange.x,
            weaponDamageScaleRange.y,
            GetDamageQuality01(package));

        float recoilQuality = GetRecoilQuality01(package);
        linearRecoilIntensityScale = Mathf.Lerp(
            linearRecoilIntensityScaleRange.x,
            linearRecoilIntensityScaleRange.y,
            recoilQuality);
        rotationalRecoilIntensityScale = Mathf.Lerp(
            rotationalRecoilIntensityScaleRange.x,
            rotationalRecoilIntensityScaleRange.y,
            recoilQuality);
        recoilIntensityScale = (linearRecoilIntensityScale + rotationalRecoilIntensityScale) * 0.5f;
        recoilReturnTimeScale = Mathf.Lerp(
            recoilReturnTimeScaleRange.x,
            recoilReturnTimeScaleRange.y,
            recoilQuality);
    }

    private float GetRecoilQuality01(GeneratedWeaponPackage package)
    {
        int recoilScore = Mathf.Clamp(package.slideLength, 0, 2)
                        + Mathf.Clamp(package.gripSize, 0, 1)
                        + Mathf.Clamp(package.slideRarity, 0, 2)
                        + Mathf.Clamp(package.gripRarity, 0, 1);

        return Mathf.Clamp01(recoilScore / 6f);
    }

    private float GetProjectileQuality01(GeneratedWeaponPackage package)
    {
        int projectileScore = Mathf.Clamp(package.slideLength, 0, 2)
                            + Mathf.Clamp(package.slideRarity, 0, 2);

        return Mathf.Clamp01(projectileScore / 4f);
    }

    private float GetDamageQuality01(GeneratedWeaponPackage package)
    {
        int damageScore = Mathf.Clamp(package.slideLength, 0, 2)
                        + Mathf.Clamp(package.slideRarity, 0, 2);

        return Mathf.Clamp01(damageScore / 4f);
    }

    private void ApplyGeneratedStatsToWeapon()
    {
        if (raycastWeapon == null)
        {
            raycastWeapon = GetComponentInChildren<RaycastWeapon>(true);
        }

        if (recoilGrabbable == null && raycastWeapon != null)
        {
            recoilGrabbable = raycastWeapon.GetComponent<Grabbable>();
        }

        if (raycastWeapon != null)
        {
            if (isBasic)
            {
                ApplyGeneratedMuzzlePoint(raycastWeapon);
                raycastWeapon.ShotForce = baseShotForce * projectileVelocityScale;
                raycastWeapon.Damage = baseWeaponDamage * weaponDamageScale;
                raycastWeapon.RecoilForce = baseLinearRecoilForce * linearRecoilIntensityScale * 1.2f;
                raycastWeapon.RecoilForceTwoHanded = baseLinearRecoilForceTwoHanded * linearRecoilIntensityScale;
                raycastWeapon.RotationalRecoilForce = baseRotationalRecoilForce * rotationalRecoilIntensityScale * 1.2f;
                raycastWeapon.RotationalRecoilForceTwoHanded = baseRotationalRecoilForceTwoHanded * rotationalRecoilIntensityScale;
                raycastWeapon.RecoilDuration = baseRecoilDuration * recoilReturnTimeScale * 1.2f;
                raycastWeapon.RecoilAngularReturnSpring = baseRotationalReturnSpring / Mathf.Max(0.01f, recoilReturnTimeScale);
                raycastWeapon.RecoilAngularReturnDamper = baseRotationalReturnDamper * recoilReturnTimeScale;
                ApplyGeneratedMagazineCapacity(raycastWeapon);
            } else if (isAuto)
            {
                ApplyGeneratedMuzzlePoint(raycastWeapon);
                raycastWeapon.ShotForce = baseShotForce * projectileVelocityScale;
                raycastWeapon.Damage = baseWeaponDamage * weaponDamageScale;
                raycastWeapon.RecoilForce = baseLinearRecoilForce * linearRecoilIntensityScale * 0.5f;
                raycastWeapon.RecoilForceTwoHanded = baseLinearRecoilForceTwoHanded * linearRecoilIntensityScale;
                raycastWeapon.RotationalRecoilForce = baseRotationalRecoilForce * rotationalRecoilIntensityScale * 0.5f;
                raycastWeapon.RotationalRecoilForceTwoHanded = baseRotationalRecoilForceTwoHanded * rotationalRecoilIntensityScale;
                raycastWeapon.RecoilDuration = baseRecoilDuration * recoilReturnTimeScale* 0.5f;
                raycastWeapon.RecoilAngularReturnSpring = baseRotationalReturnSpring / Mathf.Max(0.01f, recoilReturnTimeScale);
                raycastWeapon.RecoilAngularReturnDamper = baseRotationalReturnDamper * recoilReturnTimeScale;
                raycastWeapon.FiringMethod = FiringType.Automatic;
                ApplyGeneratedMagazineCapacity(raycastWeapon);
            } else
            {
                ApplyGeneratedMuzzlePoint(raycastWeapon);
                raycastWeapon.ShotForce = baseShotForce * projectileVelocityScale;
                raycastWeapon.Damage = baseWeaponDamage * weaponDamageScale;
                raycastWeapon.RecoilForce = baseLinearRecoilForce * linearRecoilIntensityScale;
                raycastWeapon.RecoilForceTwoHanded = baseLinearRecoilForceTwoHanded * linearRecoilIntensityScale;
                raycastWeapon.RotationalRecoilForce = baseRotationalRecoilForce * rotationalRecoilIntensityScale;
                raycastWeapon.RotationalRecoilForceTwoHanded = baseRotationalRecoilForceTwoHanded * rotationalRecoilIntensityScale;
                raycastWeapon.RecoilDuration = baseRecoilDuration * recoilReturnTimeScale;
                raycastWeapon.RecoilAngularReturnSpring = baseRotationalReturnSpring / Mathf.Max(0.01f, recoilReturnTimeScale);
                raycastWeapon.RecoilAngularReturnDamper = baseRotationalReturnDamper * recoilReturnTimeScale;
                ApplyGeneratedMagazineCapacity(raycastWeapon);
            }
            
        }

        if (recoilGrabbable != null)
        {
            recoilGrabbable.CollisionSlerp = baseRotationalReturnSpring / Mathf.Max(0.01f, recoilReturnTimeScale);
        }
    }

    private void ApplyGeneratedMuzzlePoint(RaycastWeapon weapon)
    {
        string muzzleName = Mathf.Clamp(generatedPackage.slideLength, 0, 2) == 2
            ? "MuzzlePoint_Long"
            : "MuzzlePoint";

        Transform muzzlePoint = FindChildByName(transform, muzzleName);
        if (muzzlePoint != null)
        {
            weapon.MuzzlePointTransform = muzzlePoint;
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByName(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void ApplyGeneratedMagazineCapacity(RaycastWeapon weapon)
    {
        int capacity = CurrentMagazineCapacity;
        magazineSize = capacity;

        if (weapon.ReloadMethod == ReloadType.InternalAmmo)
        {
            weapon.MaxInternalAmmo = capacity;
            weapon.InternalAmmo = capacity;
        }

        BNG.Magazine[] bngMagazines = weapon.GetComponentsInChildren<BNG.Magazine>(true);
        foreach (BNG.Magazine magazine in bngMagazines)
        {
            magazine.MaxBulletCount = capacity;
            magazine.CurrentBulletCount = GetLoadedMagazineCount(weapon, capacity);
            magazine.UpdateAllBulletGraphics();
        }

        WeaponMagazine[] weaponMagazines = weapon.GetComponentsInChildren<WeaponMagazine>(true);
        foreach (WeaponMagazine magazine in weaponMagazines)
        {
            magazine.SetCapacity(capacity);
        }
    }

    private int GetLoadedMagazineCount(RaycastWeapon weapon, int capacity)
    {
        if (weapon.MustChamberRounds && weapon.BulletInChamber)
        {
            return Mathf.Max(0, capacity - 1);
        }

        return capacity;
    }
}
