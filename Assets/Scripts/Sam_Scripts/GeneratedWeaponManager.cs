using UnityEngine;
using System.Collections.Generic;
using BNG;

public class GeneratedWeaponManager : MonoBehaviour
{
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
    public int magazineSize = 0;
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

    public RGBMaterial potentialRGBColorSlide;

    //The normal slide and the non-compact grip are the origin transform.
    //Different slide lengths require moving the frame in comparison
    // the compact grip requires moving the frame up
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        generatedPackage = GenerateWeaponPackage(rarityTier);
        ApplyGeneratedPackage(generatedPackage);
        CacheGeneratedStats(generatedPackage);
        ApplyGeneratedStatsToWeapon();
    }

    private GeneratedWeaponPackage GenerateWeaponPackage(WeaponRarityTier tier)
    {
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
                return Random.Range(0, 3);
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
        magazineSize = 0;
        projectileVelocityScale = Mathf.Lerp(
            projectileVelocityScaleRange.x,
            projectileVelocityScaleRange.y,
            GetProjectileQuality01(package));

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
            raycastWeapon.ShotForce = baseShotForce * projectileVelocityScale;
            raycastWeapon.RecoilForce = baseLinearRecoilForce * linearRecoilIntensityScale;
            raycastWeapon.RecoilForceTwoHanded = baseLinearRecoilForceTwoHanded * linearRecoilIntensityScale;
            raycastWeapon.RotationalRecoilForce = baseRotationalRecoilForce * rotationalRecoilIntensityScale;
            raycastWeapon.RotationalRecoilForceTwoHanded = baseRotationalRecoilForceTwoHanded * rotationalRecoilIntensityScale;
            raycastWeapon.RecoilDuration = baseRecoilDuration * recoilReturnTimeScale;
            raycastWeapon.RecoilAngularReturnSpring = baseRotationalReturnSpring / Mathf.Max(0.01f, recoilReturnTimeScale);
            raycastWeapon.RecoilAngularReturnDamper = baseRotationalReturnDamper * recoilReturnTimeScale;
        }

        if (recoilGrabbable != null)
        {
            recoilGrabbable.CollisionSlerp = baseRotationalReturnSpring / Mathf.Max(0.01f, recoilReturnTimeScale);
        }
    }
}
