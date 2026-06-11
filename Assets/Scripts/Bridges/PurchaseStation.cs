using System.Collections.Generic;
using BNG;
using JerryScripts.Core.PlayerState;
using TMPro;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shop-room purchase station. Spawns a healing item and a generated weapon as previews
/// (kinematic, non-grabbable until purchased) at two designer-placed spawn points. Two BNG <see cref="Button"/>s
/// (one per slot) commit each purchase via UnityEvent wiring.
///
/// <para><b>Inspector wiring</b>:</para>
/// <list type="number">
///   <item>Drag the healing-item prefab into <c>_healingItemPrefab</c></item>
///   <item>Drop an empty GameObject child for the healing spawn point; drag into <c>_healingSpawnPoint</c></item>
///   <item>Drag the weapon prefab (with <c>GeneratedWeaponManager</c>) into <c>_weaponPrefab</c></item>
///   <item>Drop a child GO for the weapon spawn point; drag into <c>_weaponSpawnPoint</c></item>
///   <item>Drop two BNG Button prefabs (or use <c>Button.prefab</c> from BNG framework) below each item.
///         On each button's <c>onButtonDown</c> UnityEvent, drag THIS PurchaseStation GameObject
///         and select either <c>OnHealingPurchaseButtonPressed</c> or <c>OnWeaponPurchaseButtonPressed</c>.</item>
///   <item>Drag each BNG Button GameObject into <c>_healingButtonAnchor</c> / <c>_weaponButtonAnchor</c>
///         to enable floating price labels.</item>
/// </list>
///
/// <para><b>Weapon rarity tier</b>: defaults to the Inspector fallback, but if a
/// <see cref="RefactoredDungeonGenerationManager"/> exists, the station maps the
/// current dungeon level to Common / Rare / Epic before the weapon rolls parts.</para>
///
/// <para><b>Asmdef boundary note</b>: lives in default Assembly-CSharp because it
/// references BNG <see cref="Button"/>, <see cref="Grabbable"/>, and Sam's
/// <c>GeneratedWeaponManager</c>.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PurchaseStation : MonoBehaviour
{
    [Header("Healing Slot")]
    [Tooltip("Prefab of the consumable healing item. Should have a HealingItem component.")]
    [SerializeField] private GameObject _healingItemPrefab;

    [Tooltip("World-space anchor where the healing preview spawns.")]
    [SerializeField] private Transform _healingSpawnPoint;

    [Tooltip("Currency cost to purchase the healing item.")]
    [Min(0)]
    [SerializeField] private int _healingCost = 50;

    [Header("Weapon Slot")]
    [Tooltip("Prefab of the generated weapon (must contain GeneratedWeaponManager).")]
    [SerializeField] private GameObject _weaponPrefab;

    [Tooltip("World-space anchor where the weapon preview spawns.")]
    [SerializeField] private Transform _weaponSpawnPoint;

    [Tooltip("Currency cost to purchase the weapon.")]
    [Min(0)]
    [SerializeField] private int _weaponCost = 100;

    [Header("Weapon Slot 2")]
    [Tooltip("Optional second generated weapon prefab. Supports GeneratedWeaponManager or GeneratedShotgunManager.")]
    [SerializeField] private GameObject _weapon2Prefab;

    [Tooltip("World-space anchor where the second weapon preview spawns.")]
    [SerializeField] private Transform _weapon2SpawnPoint;

    [Tooltip("Currency cost to purchase the second weapon.")]
    [Min(0)]
    [SerializeField] private int _weapon2Cost = 100;

    [Header("Weapon Rarity")]
    [Tooltip("Fallback tier if no RefactoredDungeonGenerationManager exists in the scene.")]
    [SerializeField] private GeneratedWeaponManager.WeaponRarityTier _weaponRarityTier = GeneratedWeaponManager.WeaponRarityTier.Common;

    [Header("Price Labels")]
    [Tooltip("BNG Button GameObject below the healing slot. Used as anchor for the floating " +
             "price label above the button. Drag the same Button GO that's wired to " +
             "OnHealingPurchaseButtonPressed via the BNG onButtonDown UnityEvent.")]
    [SerializeField] private Transform _healingButtonAnchor;

    [Tooltip("Same as above but for the weapon button.")]
    [SerializeField] private Transform _weaponButtonAnchor;

    [Tooltip("Same as above but for the second weapon button.")]
    [SerializeField] private Transform _weapon2ButtonAnchor;

    [Tooltip("Local offset from the button anchor where the price label sits. " +
             "Default (0, 0.3, 0) places it 30 cm above the button.")]
    [SerializeField] private Vector3 _priceLabelLocalOffset = new Vector3(0f, 0.3f, 0f);

    [Tooltip("Physical height of the price label panel in metres.")]
    [Min(0.02f)]
    [SerializeField] private float _priceLabelWorldHeight = 0.15f;

    [Tooltip("Font size in canvas units (scaled by _priceLabelWorldHeight to physical size).")]
    [Min(8)]
    [SerializeField] private int _priceLabelFontSize = 64;

    [Tooltip("Available-for-purchase text color.")]
    [SerializeField] private Color _priceLabelColor = new Color(1f, 0.95f, 0.4f, 1f);  // warm gold

    [Tooltip("Sold text color (post-purchase).")]
    [SerializeField] private Color _soldLabelColor = new Color(0.55f, 0.55f, 0.55f, 1f);  // gray

    [Tooltip("Background panel color behind the price text. Alpha is overridden by _priceLabelBgOpacity.")]
    [SerializeField] private Color _priceLabelBgColor = new Color(0.05f, 0.05f, 0.05f, 1f);  // near-black

    [Tooltip("Background panel opacity (0 = invisible, 1 = fully opaque). " +
             "Set to 0 to disable the background entirely.")]
    [Range(0f, 1f)]
    [SerializeField] private float _priceLabelBgOpacity = 0.65f;

    [Tooltip("BG panel width as a fraction of the label canvas width. " +
             "1.0 = full width (current behavior); 0.5 = half-width centered. " +
             "Tune down if the dark panel looks wider than the text.")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float _priceLabelBgWidthFraction = 0.5f;

    [Tooltip("BG panel height as a fraction of the label canvas height. " +
             "1.0 = full height (current behavior). Tune down for a thinner panel.")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float _priceLabelBgHeightFraction = 1.0f;

    [Tooltip("Text shown for an available item. {0} substitutes the cost.")]
    [SerializeField] private string _priceLabelFormat = "$ {0}";

    [Tooltip("Text shown after the slot is purchased.")]
    [SerializeField] private string _soldLabelText = "SOLD";

    [SerializeField] private List<GameObject> shotguns;

    private GameObject _spawnedHealing;
    private GameObject _spawnedWeapon;
    private GameObject _spawnedWeapon2;
    private bool _healingPurchased;
    private bool _weaponPurchased;
    private bool _weapon2Purchased;

    private TextMeshProUGUI _healingPriceText;
    private TextMeshProUGUI _weaponPriceText;
    private TextMeshProUGUI _weapon2PriceText;
    private GameObject _healingPriceLabel;
    private GameObject _weaponPriceLabel;
    private GameObject _weapon2PriceLabel;
    private Camera _cachedCamera;

    private void Start()
    {
        SpawnHealingPreview();
        SpawnWeaponPreview();
        SpawnWeapon2Preview();
        BuildPriceLabels();
    }

    // ============================================================================
    // Spawn (preview = kinematic, non-grabbable until purchased)
    // ============================================================================

    private void SpawnHealingPreview()
    {
        if (_healingItemPrefab == null || _healingSpawnPoint == null)
        {
            Debug.LogWarning("[PurchaseStation] Healing slot prefab or spawn point is null — slot disabled.", this);
            return;
        }

        _spawnedHealing = Instantiate(
            _healingItemPrefab,
            _healingSpawnPoint.position,
            _healingSpawnPoint.rotation);
        SetItemInteractable(_spawnedHealing, interactable: false);
    }

    private void SpawnWeaponPreview()
    {
        if (_weaponPrefab == null || _weaponSpawnPoint == null)
        {
            Debug.LogWarning("[PurchaseStation] Weapon slot prefab or spawn point is null — slot disabled.", this);
            return;
        }

        // Inactive-staging trick: instantiate as a child of an inactive GO so the
        // weapon's Awake/Start don't run yet. Set rarityTier on GeneratedWeaponManager
        // BEFORE generation, then move out of staging to trigger the lifecycle.
        GameObject staging = new GameObject("PurchaseStaging_Weapon");
        staging.SetActive(false);

        _spawnedWeapon = Instantiate(_weaponPrefab, staging.transform);

        var mgr = _spawnedWeapon.GetComponent<GeneratedWeaponManager>();
        if (mgr != null)
        {
            mgr.rarityTier = ResolveWeaponRarityTier();
        }
        else
        {
            Debug.LogWarning(
                "[PurchaseStation] Weapon prefab has no GeneratedWeaponManager — " +
                "rarity tier won't be applied.",
                this);
        }

        // Move out of staging — parent activates the new GO, Awake/Start run with
        // the rarity tier already set.
        _spawnedWeapon.transform.SetParent(_weaponSpawnPoint, worldPositionStays: false);
        _spawnedWeapon.transform.localPosition = Vector3.zero;
        _spawnedWeapon.transform.localRotation = Quaternion.identity;

        Destroy(staging);

        SetItemInteractable(_spawnedWeapon, interactable: false);
    }

    private void SpawnWeapon2Preview()
    {
        if (_weapon2Prefab == null || _weapon2SpawnPoint == null)
        {
            Debug.LogWarning("[PurchaseStation] Weapon 2 slot prefab or spawn point is null - slot disabled.", this);
            return;
        }

        GameObject staging = new GameObject("PurchaseStaging_Weapon2");
        staging.SetActive(false);

        if (ResolveWeaponRarityTier() == GeneratedWeaponManager.WeaponRarityTier.Common)
        {
            _spawnedWeapon2 = Instantiate(shotguns[0], staging.transform);
            Debug.Log("starter shotgun" + _spawnedWeapon2.name);
        } else if (ResolveWeaponRarityTier() == GeneratedWeaponManager.WeaponRarityTier.Rare)
        {
            _spawnedWeapon2 = Instantiate(shotguns[1], staging.transform);
            Debug.Log("mid shotgun" + _spawnedWeapon2.name);
        } else
        {
           _spawnedWeapon2 = Instantiate(shotguns[2], staging.transform);
           Debug.Log("best shotgun" + _spawnedWeapon2.name);
        }

        //_spawnedWeapon2 = Instantiate(_weapon2Prefab, staging.transform);
        // GeneratedWeaponManager.WeaponRarityTier rarityTier = ResolveWeaponRarityTier();

        // var generatedWeaponManager = _spawnedWeapon2.GetComponent<GeneratedWeaponManager>();
        // if (generatedWeaponManager != null)
        // {
        //     generatedWeaponManager.rarityTier = rarityTier;
        // }

        // var generatedShotgunManager = _spawnedWeapon2.GetComponent<GeneratedShotgunManager>();
        // if (generatedShotgunManager != null)
        // {
        //     generatedShotgunManager.rarityTier = rarityTier;
        // }

        // if (generatedWeaponManager == null && generatedShotgunManager == null)
        // {
        //     Debug.LogWarning(
        //         "[PurchaseStation] Weapon 2 prefab has no generated weapon manager - rarity tier won't be applied.",
        //         this);
        // }

        _spawnedWeapon2.transform.SetParent(_weapon2SpawnPoint, worldPositionStays: false);
        _spawnedWeapon2.transform.localPosition = Vector3.zero;
        _spawnedWeapon2.transform.localRotation = Quaternion.identity;

        Destroy(staging);

        SetItemInteractable(_spawnedWeapon2, interactable: false);
    }

    private GeneratedWeaponManager.WeaponRarityTier ResolveWeaponRarityTier()
    {
        RefactoredDungeonGenerationManager dungeonGenerationManager = FindAnyObjectByType<RefactoredDungeonGenerationManager>();
        if (dungeonGenerationManager == null)
        {
            return _weaponRarityTier;
        }

        return dungeonGenerationManager.GetWeaponRarityTierForCurrentDungeon();
    }

    /// <summary>
    /// Toggles whether a spawned shop item is grabbable + physics-enabled.
    /// Preview state: <paramref name="interactable"/> = false (kinematic, Grabbable disabled).
    /// Purchased state: true (gravity on, Grabbable enabled).
    /// </summary>
    private static void SetItemInteractable(GameObject item, bool interactable)
    {
        if (item == null) return;

        var grabbable = item.GetComponent<Grabbable>();
        if (grabbable != null) grabbable.enabled = interactable;

        var rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = !interactable;
            rb.useGravity = interactable;
        }
    }

    // ============================================================================
    // Price labels (floating world-space text above each button)
    // ============================================================================

    private void BuildPriceLabels()
    {
        if (_healingButtonAnchor != null)
        {
            _healingPriceLabel = BuildPriceLabel(
                _healingButtonAnchor,
                string.Format(_priceLabelFormat, _healingCost),
                "PriceLabel_Healing");
            _healingPriceText = _healingPriceLabel.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (_weaponButtonAnchor != null)
        {
            _weaponPriceLabel = BuildPriceLabel(
                _weaponButtonAnchor,
                string.Format(_priceLabelFormat, _weaponCost),
                "PriceLabel_Weapon");
            _weaponPriceText = _weaponPriceLabel.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (_weapon2ButtonAnchor != null)
        {
            _weapon2PriceLabel = BuildPriceLabel(
                _weapon2ButtonAnchor,
                string.Format(_priceLabelFormat, _weapon2Cost),
                "PriceLabel_Weapon2");
            _weapon2PriceText = _weapon2PriceLabel.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private GameObject BuildPriceLabel(Transform anchor, string initialText, string goName)
    {
        GameObject root = new GameObject(goName);
        root.transform.SetParent(anchor, worldPositionStays: false);
        root.transform.localPosition = _priceLabelLocalOffset;
        root.transform.localRotation = Quaternion.identity;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        const float canvasW = 600f;
        const float canvasH = 200f;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(canvasW, canvasH);
        // Scale uniformly so canvasH = _priceLabelWorldHeight in metres
        float scale = _priceLabelWorldHeight / canvasH;
        canvasRect.localScale = new Vector3(scale, scale, scale);

        // Background panel — only build if opacity > 0 (Inspector toggle)
        if (_priceLabelBgOpacity > 0f)
        {
            GameObject bgGO = new GameObject("BG", typeof(RectTransform));
            bgGO.transform.SetParent(root.transform, worldPositionStays: false);

            Image bg = bgGO.AddComponent<Image>();
            Color bgC = _priceLabelBgColor;
            bgC.a = _priceLabelBgOpacity;
            bg.color = bgC;
            bg.raycastTarget = false;

            RectTransform bgRect = bgGO.GetComponent<RectTransform>();
            float halfW = _priceLabelBgWidthFraction * 0.5f;
            float halfH = _priceLabelBgHeightFraction * 0.5f;
            bgRect.anchorMin = new Vector2(0.5f - halfW, 0.5f - halfH);
            bgRect.anchorMax = new Vector2(0.5f + halfW, 0.5f + halfH);
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        }

        // Text child fills the canvas
        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(root.transform, worldPositionStays: false);

        TextMeshProUGUI t = textGO.AddComponent<TextMeshProUGUI>();
        t.text = initialText;
        t.fontSize = _priceLabelFontSize;
        t.color = _priceLabelColor;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;

        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        return root;
    }

    private void LateUpdate()
    {
        if (_cachedCamera == null)
        {
            _cachedCamera = Camera.main;
            if (_cachedCamera == null) return;
        }
        BillboardLabel(_healingPriceLabel);
        BillboardLabel(_weaponPriceLabel);
        BillboardLabel(_weapon2PriceLabel);
    }

    private void BillboardLabel(GameObject label)
    {
        if (label == null) return;
        Transform camT = _cachedCamera.transform;
        label.transform.rotation = Quaternion.LookRotation(
            label.transform.position - camT.position,
            Vector3.up);
    }

    private void MarkSlotSold(TextMeshProUGUI priceText)
    {
        if (priceText == null) return;
        priceText.text = _soldLabelText;
        priceText.color = _soldLabelColor;
    }

    // ============================================================================
    // Purchase handlers (wired to BNG Button onButtonDown UnityEvent)
    // ============================================================================

    public void OnHealingPurchaseButtonPressed()
    {
        TryPurchase(
            slotName: "healing",
            cost: _healingCost,
            isAlreadyPurchased: _healingPurchased,
            spawnedItem: _spawnedHealing,
            priceText: _healingPriceText,
            onSuccess: () => _healingPurchased = true);
    }

    public void OnWeaponPurchaseButtonPressed()
    {
        TryPurchase(
            slotName: "weapon",
            cost: _weaponCost,
            isAlreadyPurchased: _weaponPurchased,
            spawnedItem: _spawnedWeapon,
            priceText: _weaponPriceText,
            onSuccess: () => _weaponPurchased = true);
    }

    public void OnWeapon2PurchaseButtonPressed()
    {
        TryPurchase(
            slotName: "weapon 2",
            cost: _weapon2Cost,
            isAlreadyPurchased: _weapon2Purchased,
            spawnedItem: _spawnedWeapon2,
            priceText: _weapon2PriceText,
            onSuccess: () => _weapon2Purchased = true);
    }

    private void TryPurchase(string slotName, int cost, bool isAlreadyPurchased, GameObject spawnedItem, TextMeshProUGUI priceText, System.Action onSuccess)
    {
        if (isAlreadyPurchased)
        {
            Debug.Log($"[PurchaseStation] {slotName} slot already purchased — ignored.", this);
            return;
        }

        if (spawnedItem == null)
        {
            Debug.LogWarning($"[PurchaseStation] {slotName} slot has no spawned item — purchase ignored.", this);
            return;
        }

        var psm = FindAnyObjectByType<PlayerStateManager>();
        if (psm == null)
        {
            Debug.LogWarning("[PurchaseStation] No PlayerStateManager — purchase aborted.", this);
            return;
        }

        bool spent = psm.SpendCurrency(cost);
        if (!spent)
        {
            Debug.Log(
                $"[PurchaseStation] Insufficient funds for {slotName} (have {psm.CurrentCurrency}, need {cost}).",
                this);
            return;
        }

        // Release the item — make it grabbable + physics-active.
        SetItemInteractable(spawnedItem, interactable: true);
        onSuccess?.Invoke();
        MarkSlotSold(priceText);
        Debug.Log($"[PurchaseStation] {slotName} purchased for {cost}.", this);
    }
}
