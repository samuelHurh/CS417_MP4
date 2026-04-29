using JerryScripts.Core.PlayerState;
using JerryScripts.Feature.WeaponHandling;
using JerryScripts.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace JerryScripts.Presentation.HUD
{
    /// <summary>
    /// Hand-mounted HUD system. Manages three display states:
    ///
    /// <para><b>Running (HUD-01/02/05):</b> Segmented health bar, currency counter,
    /// and ammo count on a world-space Canvas parented to the left controller.</para>
    ///
    /// <para><b>Paused (HUD-03):</b> Same hand Canvas swaps to show button prompts.
    /// Player uses physical controller buttons to Resume/Restart/Quit.
    /// Trigger=Resume, PrimaryButton=Restart, SecondaryButton=Quit.</para>
    ///
    /// <para><b>Dead (HUD-04):</b> Large floating panel at gaze-center (0.8m forward
    /// from head) with button prompts. Trigger=Restart, PrimaryButton=Quit.</para>
    ///
    /// <para><b>Weapon panel (HUD-06 / Block B, S2-009):</b> A second canvas
    /// parented directly below Block A on the same left-controller anchor.
    /// Shows rarity name (rarity color), pistol silhouette icon, and 5 stat bars
    /// (DMG / RPM / MAG / REC / VEL). Visible only when a weapon is held in
    /// Running state. Refreshed atomically on <see cref="WeaponInstance.OnEquipChanged"/>.
    /// Bar fills use the normalization formulas in weapon-generation.md §Stat-Bar Normalization.
    /// No per-frame recomputation — WeaponData is immutable after generation.</para>
    ///
    /// <para><b>Interaction model:</b> No XR ray/poke UI interaction needed.
    /// Menu actions are bound to physical controller buttons via InputActionReferences,
    /// the same pattern used by WeaponInstance. The on-screen labels tell the player
    /// which button does what.</para>
    /// </summary>
    /// <remarks>
    /// S2-003 + S2-004 + S2-009. GDD: ui-hud-system.md, weapon-generation.md §UI Requirements.
    /// Architecture: Presentation layer — consumes Foundation + Core + Feature.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HUDSystem : MonoBehaviour
    {
        // ===================================================================
        // Inspector fields
        // ===================================================================

        [Header("Config")]
        [SerializeField] private HUDConfig _config;

        [Header("Dependencies")]
        [SerializeField] private PlayerStateManager _playerStateManager;

        [Header("Menu Input — Left Hand (used when paused/dead)")]
        [Tooltip("Trigger action on the left hand — Resume (paused) / Restart (dead).")]
        [SerializeField] private InputActionReference _leftTriggerAction;

        [Tooltip("Primary button (X) on the left hand — Restart (paused) / Quit (dead).")]
        [SerializeField] private InputActionReference _leftPrimaryAction;

        [Tooltip("Secondary button (Y) on the left hand — Quit (paused only).")]
        [SerializeField] private InputActionReference _leftSecondaryAction;

        [Header("Menu Input — Right Hand (used when paused/dead)")]
        [Tooltip("Trigger action on the right hand — Resume (paused) / Restart (dead).")]
        [SerializeField] private InputActionReference _rightTriggerAction;

        [Tooltip("Primary button (A) on the right hand — Restart (paused) / Quit (dead).")]
        [SerializeField] private InputActionReference _rightPrimaryAction;

        [Tooltip("Secondary button (B) on the right hand — Quit (paused only).")]
        [SerializeField] private InputActionReference _rightSecondaryAction;

        // ===================================================================
        // Constants
        // ===================================================================

        private const int HealthSegmentCount = 10;

        // ===================================================================
        // Resolved references
        // ===================================================================

        private IPlayerStateReader _stateReader;
        private IPlayerStateWriter _stateWriter;
        private IRigControllerProvider _rigControllerProvider;
        private PlayerRig _playerRig;
        private WeaponInstance _weaponInstance;

        // ===================================================================
        // UI — hand display (shared canvas for Running + Paused)
        // ===================================================================

        private GameObject _handDisplayRoot;
        private GameObject _runningPanel;
        private Image[] _healthSegments;
        private Text _healthText;
        private Text _currencyText;
        private Text _ammoText;
        private GameObject _pausePanel;

        // ===================================================================
        // UI — weapon panel (HUD-06, Block B)
        // ===================================================================

        /// <summary>Root canvas GameObject for Block B. Hidden by default.</summary>
        private GameObject _weaponBlockB;
        private Text _rarityNameText;
        private Image _pistolSilhouetteImage;
        private Image _barFillDmg;
        private Image _barFillRpm;
        private Image _barFillMag;
        private Image _barFillRec;
        private Image _barFillVel;

        // ===================================================================
        // UI — floating panels (separate canvases at gaze-center)
        // ===================================================================

        private GameObject _pauseScreenRoot;
        private GameObject _deathScreenRoot;

        // ===================================================================
        // State tracking for menu input
        // ===================================================================

        private PlayerState _currentDisplayState = PlayerState.Running;
        private float _awakeTime;

        // Cached
        private Color _segmentFilledColor;
        private Color _segmentEmptyColor;
        private Font _hudFont;
        private int _fontSize;
        private float _padding;
        private Color _textColor;
        private Color _bgColor;
        private float _bgOpacity;

        // ===================================================================
        // Unity lifecycle
        // ===================================================================

        private void Awake()
        {
            _awakeTime = Time.unscaledTime;
            ValidateReferences();

            if (_playerStateManager != null)
            {
                _stateReader = _playerStateManager;
                _stateWriter = _playerStateManager;
            }

            _playerRig = FindAnyObjectByType<PlayerRig>();
            if (_playerRig != null)
                _rigControllerProvider = _playerRig;

            _weaponInstance = FindAnyObjectByType<WeaponInstance>();

            ResolveSharedValues();
            BuildHandDisplay();
            BuildWeaponPanel();
            BuildPauseScreen();
            BuildDeathScreen();
        }

        private void OnEnable()
        {
            if (_stateReader != null)
            {
                _stateReader.OnHealthChanged += OnHealthChanged;
                _stateReader.OnCurrencyChanged += OnCurrencyChanged;
                _stateReader.OnStateChanged += OnStateChanged;
            }

            if (_weaponInstance != null)
            {
                _weaponInstance.OnAmmoChanged += OnAmmoChanged;
                _weaponInstance.OnEquipChanged += OnEquipChanged;
            }

            SubscribeMenuInput();
            SyncToCurrentState();
        }

        private void OnDisable()
        {
            if (_stateReader != null)
            {
                _stateReader.OnHealthChanged -= OnHealthChanged;
                _stateReader.OnCurrencyChanged -= OnCurrencyChanged;
                _stateReader.OnStateChanged -= OnStateChanged;
            }

            if (_weaponInstance != null)
            {
                _weaponInstance.OnAmmoChanged -= OnAmmoChanged;
                _weaponInstance.OnEquipChanged -= OnEquipChanged;
            }

            UnsubscribeMenuInput();
        }

        // ===================================================================
        // Menu input subscriptions
        // ===================================================================

        private void SubscribeMenuInput()
        {
            SubscribeAction(_leftTriggerAction, OnMenuTrigger);
            SubscribeAction(_leftPrimaryAction, OnMenuPrimary);
            SubscribeAction(_leftSecondaryAction, OnMenuSecondary);
            SubscribeAction(_rightTriggerAction, OnMenuTrigger);
            SubscribeAction(_rightPrimaryAction, OnMenuPrimary);
            SubscribeAction(_rightSecondaryAction, OnMenuSecondary);
        }

        private void UnsubscribeMenuInput()
        {
            UnsubscribeAction(_leftTriggerAction, OnMenuTrigger);
            UnsubscribeAction(_leftPrimaryAction, OnMenuPrimary);
            UnsubscribeAction(_leftSecondaryAction, OnMenuSecondary);
            UnsubscribeAction(_rightTriggerAction, OnMenuTrigger);
            UnsubscribeAction(_rightPrimaryAction, OnMenuPrimary);
            UnsubscribeAction(_rightSecondaryAction, OnMenuSecondary);
        }

        private static void SubscribeAction(InputActionReference actionRef,
            System.Action<InputAction.CallbackContext> handler)
        {
            if (actionRef == null) return;
            actionRef.action.Enable();
            actionRef.action.performed += handler;
        }

        private static void UnsubscribeAction(InputActionReference actionRef,
            System.Action<InputAction.CallbackContext> handler)
        {
            if (actionRef == null) return;
            actionRef.action.performed -= handler;
        }

        // ===================================================================
        // Menu input handlers — actions depend on current state
        // ===================================================================

        /// <summary>
        /// Returns true if menu input should be ignored — blocks stale button
        /// events from a previous scene carrying over after SceneManager.LoadScene.
        /// </summary>
        private bool IsMenuInputBlocked()
        {
            return Time.unscaledTime - _awakeTime < 1.0f;
        }

        private void OnMenuTrigger(InputAction.CallbackContext _)
        {
            if (IsMenuInputBlocked()) return;

            switch (_currentDisplayState)
            {
                case PlayerState.Paused:
                    _stateWriter?.RequestResume();
                    break;
                case PlayerState.Dead:
                    _stateWriter?.RequestRestart();
                    break;
            }
        }

        private void OnMenuPrimary(InputAction.CallbackContext _)
        {
            if (IsMenuInputBlocked()) return;

            switch (_currentDisplayState)
            {
                case PlayerState.Paused:
                    _stateWriter?.RequestRestart();
                    break;
                case PlayerState.Dead:
                    _stateWriter?.RequestQuit();
                    break;
            }
        }

        private void OnMenuSecondary(InputAction.CallbackContext _)
        {
            if (IsMenuInputBlocked()) return;

            if (_currentDisplayState == PlayerState.Paused)
                _stateWriter?.RequestQuit();
        }

        // ===================================================================
        // Event handlers
        // ===================================================================

        private void OnHealthChanged(float newHealth)
        {
            if (_stateReader == null) return;

            float maxHealth = _stateReader.MaxHealth;
            float fillRatio = maxHealth > 0f ? Mathf.Clamp01(newHealth / maxHealth) : 0f;

            if (_healthSegments != null)
            {
                int filledCount = Mathf.CeilToInt(fillRatio * HealthSegmentCount);
                for (int i = 0; i < _healthSegments.Length; i++)
                    if (_healthSegments[i] != null)
                        _healthSegments[i].color = i < filledCount ? _segmentFilledColor : _segmentEmptyColor;
            }

            if (_healthText != null)
                _healthText.text = $"{Mathf.CeilToInt(newHealth)}";
        }

        private void OnCurrencyChanged(int newCurrency)
        {
            if (_currencyText != null)
                _currencyText.text = $"$ {Mathf.Max(0, newCurrency)}";
        }

        private void OnAmmoChanged(int currentAmmo, int magCapacity)
        {
            if (_ammoText != null)
                _ammoText.text = $"{currentAmmo} / {magCapacity}";
        }

        private void OnEquipChanged(bool isEquipped)
        {
            // Block A ammo text
            if (_ammoText != null)
            {
                _ammoText.gameObject.SetActive(isEquipped);
                if (isEquipped && _weaponInstance != null)
                    OnAmmoChanged(_weaponInstance.CurrentAmmo, _weaponInstance.MagCapacity);
            }

            // Block B — weapon stat panel (HUD-06)
            if (_weaponBlockB != null)
                _weaponBlockB.SetActive(isEquipped);

            if (isEquipped && _weaponInstance != null)
                RefreshWeaponPanel(_weaponInstance.Data);
        }

        private void OnStateChanged(PlayerState newState)
        {
            _currentDisplayState = newState;

            switch (newState)
            {
                case PlayerState.Running:
                    if (_handDisplayRoot != null) _handDisplayRoot.SetActive(true);
                    if (_runningPanel != null) _runningPanel.SetActive(true);
                    if (_pausePanel != null) _pausePanel.SetActive(false);
                    if (_pauseScreenRoot != null) _pauseScreenRoot.SetActive(false);
                    if (_deathScreenRoot != null) _deathScreenRoot.SetActive(false);
                    break;

                case PlayerState.Paused:
                    if (_handDisplayRoot != null) _handDisplayRoot.SetActive(true);
                    if (_runningPanel != null) _runningPanel.SetActive(false);
                    if (_pausePanel != null) _pausePanel.SetActive(true);
                    if (_pauseScreenRoot != null) _pauseScreenRoot.SetActive(true);
                    if (_deathScreenRoot != null) _deathScreenRoot.SetActive(false);
                    break;

                case PlayerState.Dead:
                    if (_handDisplayRoot != null) _handDisplayRoot.SetActive(false);
                    if (_pauseScreenRoot != null) _pauseScreenRoot.SetActive(false);
                    if (_deathScreenRoot != null) _deathScreenRoot.SetActive(true);
                    break;
            }
        }

        // ===================================================================
        // Shared config resolution
        // ===================================================================

        private void ResolveSharedValues()
        {
            _textColor = _config != null ? _config.TextColor : new Color(0.91f, 0.91f, 0.82f, 1f);
            _bgColor   = _config != null ? _config.BgColor   : new Color(0.1f, 0.1f, 0.1f, 1f);
            _bgOpacity = _config != null ? _config.BgOpacity  : 0.7f;
            _segmentFilledColor = _config != null ? _config.HealthBarFillColor : new Color(0.2f, 0.8f, 0.3f, 1f);
            _segmentEmptyColor  = _config != null ? _config.HealthBarBgColor   : new Color(0.3f, 0.3f, 0.3f, 1f);

            _hudFont = _config != null ? _config.Font : null;
            if (_hudFont == null) _hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_hudFont == null) _hudFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_hudFont == null) _hudFont = Font.CreateDynamicFontFromOSFont("Arial", 40);

            _fontSize = 52;
            _padding = 16f;
        }

        // ===================================================================
        // Build — hand display (Running + Pause panels on same canvas)
        // ===================================================================

        private void BuildHandDisplay()
        {
            if (_rigControllerProvider == null) return;
            Transform leftController = _rigControllerProvider.LeftControllerTransform;
            if (leftController == null) return;

            Vector3 localOffset   = _config != null ? _config.LocalOffset   : new Vector3(0f, 0.08f, -0.05f);
            Vector3 localRotation = _config != null ? _config.LocalRotation : new Vector3(-30f, 0f, 0f);
            float   panelWidth    = _config != null ? _config.PanelWidth    : 0.12f;
            float   panelHeight   = _config != null ? _config.PanelHeight   : 0.04f;

            _handDisplayRoot = new GameObject("HandDisplay_HUD");
            _handDisplayRoot.transform.SetParent(leftController, false);
            _handDisplayRoot.transform.localPosition = localOffset;
            _handDisplayRoot.transform.localRotation = Quaternion.Euler(localRotation);

            Canvas canvas = _handDisplayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            float canvasW = 800f, canvasH = 270f;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(canvasW, canvasH);
            canvasRect.localScale = new Vector3(panelWidth / canvasW, panelHeight / canvasH, 1f);

            AddBgPanel(_handDisplayRoot);

            // --- Running panel ---
            _runningPanel = CreateChild(_handDisplayRoot, "RunningPanel");
            StretchFill(_runningPanel.GetComponent<RectTransform>());
            BuildRunningContent(_runningPanel);

            // --- Pause panel ---
            _pausePanel = CreateChild(_handDisplayRoot, "PausePanel");
            StretchFill(_pausePanel.GetComponent<RectTransform>());
            BuildPauseContent(_pausePanel);
            _pausePanel.SetActive(false);

            _handDisplayRoot.SetActive(false);
        }

        private void BuildRunningContent(GameObject parent)
        {
            Color barFillColor = _segmentFilledColor;

            AddText(parent, "HealthLabel", "\u2665", _fontSize, barFillColor, TextAnchor.MiddleCenter,
                0f, 0.5f, 0.08f, 1f, _padding, _padding, 0f, -_padding);

            GameObject segContainer = CreateChild(parent, "HealthSegments");
            RectTransform segRect = segContainer.GetComponent<RectTransform>();
            segRect.anchorMin = new Vector2(0.09f, 0.5f);
            segRect.anchorMax = new Vector2(0.78f, 1f);
            segRect.offsetMin = new Vector2(0f, _padding + 4f);
            segRect.offsetMax = new Vector2(0f, -_padding - 4f);

            _healthSegments = new Image[HealthSegmentCount];
            float gapFraction = 0.015f;
            float segWidth = (1f - gapFraction * (HealthSegmentCount - 1)) / HealthSegmentCount;
            for (int i = 0; i < HealthSegmentCount; i++)
            {
                float xMin = i * (segWidth + gapFraction);
                GameObject segGO = CreateChild(segContainer, $"Seg_{i}");
                Image segImg = segGO.AddComponent<Image>();
                segImg.color = barFillColor;
                segImg.raycastTarget = false;
                _healthSegments[i] = segImg;
                RectTransform sr = segGO.GetComponent<RectTransform>();
                sr.anchorMin = new Vector2(xMin, 0f);
                sr.anchorMax = new Vector2(xMin + segWidth, 1f);
                sr.offsetMin = sr.offsetMax = Vector2.zero;
            }

            _healthText = AddText(parent, "HealthText", "100", _fontSize, _textColor,
                TextAnchor.MiddleCenter, 0.80f, 0.5f, 1f, 1f, 0f, _padding, -_padding, -_padding);

            _currencyText = AddText(parent, "CurrencyText", "$ 0", _fontSize, _textColor,
                TextAnchor.MiddleLeft, 0f, 0f, 0.5f, 0.5f, _padding * 2.5f, _padding, -_padding, -_padding);

            _ammoText = AddText(parent, "AmmoText", "-- / --", _fontSize, _textColor,
                TextAnchor.MiddleRight, 0.5f, 0f, 1f, 0.5f, _padding, _padding, -_padding * 2f, -_padding);
        }

        private void BuildPauseContent(GameObject parent)
        {
            // Title — large, centered at top
            AddText(parent, "PauseTitle", "PAUSED", _fontSize + 4, _textColor,
                TextAnchor.MiddleCenter, 0f, 0.55f, 1f, 1f, _padding, 0f, -_padding, -_padding);

            // Button prompts — stacked vertically with readable text
            int promptSize = _fontSize - 4;
            Color promptColor = new Color(1f, 1f, 0.95f, 1f);

            AddText(parent, "ResumePrompt", "Trigger  RESUME", promptSize, promptColor,
                TextAnchor.MiddleCenter, 0.05f, 0.28f, 0.95f, 0.52f, 0f, 0f, 0f, 0f);

            AddText(parent, "RestartPrompt", "A / X  RESTART", promptSize, promptColor,
                TextAnchor.MiddleCenter, 0.05f, 0.02f, 0.55f, 0.26f, 0f, 0f, 0f, 0f);

            AddText(parent, "QuitPrompt", "B / Y  QUIT", promptSize, new Color(0.9f, 0.4f, 0.4f, 1f),
                TextAnchor.MiddleCenter, 0.55f, 0.02f, 0.95f, 0.26f, 0f, 0f, 0f, 0f);
        }

        // ===================================================================
        // Build — weapon stat panel (HUD-06, Block B, S2-009)
        // ===================================================================

        /// <summary>
        /// Builds the HUD-06 Block B canvas. Parented to the left controller anchor
        /// directly below Block A. Hidden by default — shown only when a weapon is held.
        ///
        /// <para>Layout (left to right across the canvas):</para>
        /// <list type="number">
        ///   <item>Rarity name text (rarity color, left column, ~20% width)</item>
        ///   <item>Pistol silhouette icon (image, left column, ~20% width)</item>
        ///   <item>Five horizontal stat bars DMG / RPM / MAG / REC / VEL (~60% width)</item>
        /// </list>
        ///
        /// <para>Bar fills use the normalization formulas in
        /// weapon-generation.md §Stat-Bar Normalization.
        /// No per-frame recomputation — <see cref="RefreshWeaponPanel"/> is called
        /// once on equip; <see cref="WeaponData"/> is immutable after generation.</para>
        /// </summary>
        private void BuildWeaponPanel()
        {
            if (_rigControllerProvider == null) return;
            Transform leftController = _rigControllerProvider.LeftControllerTransform;
            if (leftController == null) return;

            float panelWidth    = _config != null ? _config.PanelWidth         : 0.12f;
            float blockAHeight  = _config != null ? _config.PanelHeight        : 0.04f;
            float blockBHeight  = _config != null ? _config.WeaponPanelHeight  : 0.06f;

            Vector3 blockAOffset = _config != null ? _config.LocalOffset   : new Vector3(0f, 0.08f, -0.05f);
            Vector3 blockARot    = _config != null ? _config.LocalRotation : new Vector3(-30f, 0f, 0f);

            // Position Block B directly below Block A along local Y
            // (offset by the Block A height, accounting for the parent tilt)
            Vector3 blockBOffset = blockAOffset - new Vector3(0f, blockAHeight + 0.004f, 0f);

            _weaponBlockB = new GameObject("WeaponPanel_HUD");
            _weaponBlockB.transform.SetParent(leftController, false);
            _weaponBlockB.transform.localPosition = blockBOffset;
            _weaponBlockB.transform.localRotation = Quaternion.Euler(blockARot);

            Canvas canvas = _weaponBlockB.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            float canvasW = 800f, canvasH = 360f;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(canvasW, canvasH);
            canvasRect.localScale = new Vector3(panelWidth / canvasW, blockBHeight / canvasH, 1f);

            AddBgPanel(_weaponBlockB);

            // --- Left column: rarity name + silhouette icon ---

            _rarityNameText = AddText(
                _weaponBlockB, "RarityName", "Basic",
                _fontSize - 4, Color.white, TextAnchor.MiddleCenter,
                0f, 0.55f, 0.25f, 1f,
                _padding, 0f, -_padding, -_padding);

            // Pistol silhouette image
            GameObject iconGO = CreateChild(_weaponBlockB, "PistolIcon");
            Image iconImage = iconGO.AddComponent<Image>();
            iconImage.raycastTarget = false;
            Sprite silhouette = _config != null ? _config.PistolSilhouette : null;
            if (silhouette != null)
            {
                iconImage.sprite = silhouette;
                iconImage.preserveAspect = true;
            }
            else
            {
                // No sprite assigned — log warning, leave image colorless so panel still renders
                Debug.LogWarning(
                    "[HUDSystem] HUDConfig.PistolSilhouette is not assigned. " +
                    "The HUD-06 icon cell will be empty. Assign a pistol silhouette sprite " +
                    "in the HUDConfig asset. (ui-hud-system.md Rule 19)",
                    this);
                iconImage.color = new Color(0f, 0f, 0f, 0f);
            }
            _pistolSilhouetteImage = iconImage;
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(0.25f, 0.52f);
            iconRect.offsetMin = new Vector2(_padding, _padding);
            iconRect.offsetMax = new Vector2(-_padding, -_padding);

            // --- Right column: five stat bars ---
            BuildStatBars(_weaponBlockB);

            // Hidden by default — shown only when weapon is equipped (ui-hud-system.md Rule 18)
            _weaponBlockB.SetActive(false);
        }

        /// <summary>
        /// Builds the five labeled stat bar rows inside the right two-thirds of Block B.
        /// Stores fill Images in the five _barFill* fields for later update via
        /// <see cref="RefreshWeaponPanel"/>.
        /// </summary>
        private void BuildStatBars(GameObject parent)
        {
            // Bar container: right 75% of the canvas
            GameObject barsRoot = CreateChild(parent, "StatBars");
            RectTransform barsRect = barsRoot.GetComponent<RectTransform>();
            barsRect.anchorMin = new Vector2(0.26f, 0f);
            barsRect.anchorMax = new Vector2(1f, 1f);
            barsRect.offsetMin = new Vector2(_padding * 0.5f, _padding);
            barsRect.offsetMax = new Vector2(-_padding, -_padding);

            // Five bars stacked vertically, equally spaced
            string[] labels = { "DMG", "RPM", "MAG", "REC", "VEL" };
            float rowH = 1f / labels.Length;
            Color barTrackColor  = new Color(0.25f, 0.25f, 0.25f, 1f);
            Color barFillColor   = new Color(0.3f, 0.75f, 0.95f, 1f);
            int labelSize        = _fontSize - 14;

            Image[] fills = new Image[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                float yMin = 1f - (i + 1) * rowH;
                float yMax = 1f - i * rowH;

                GameObject row = CreateChild(barsRoot, $"StatRow_{labels[i]}");
                RectTransform rowRect = row.GetComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0f, yMin);
                rowRect.anchorMax = new Vector2(1f, yMax);
                rowRect.offsetMin = new Vector2(0f, 2f);
                rowRect.offsetMax = new Vector2(0f, -2f);

                // Label (left ~20%)
                AddText(row, $"Label_{labels[i]}", labels[i],
                    labelSize, _textColor, TextAnchor.MiddleLeft,
                    0f, 0f, 0.22f, 1f,
                    0f, 0f, 0f, 0f);

                // Track background (right ~78%)
                GameObject trackGO = CreateChild(row, $"Track_{labels[i]}");
                Image track = trackGO.AddComponent<Image>();
                track.color = barTrackColor;
                track.raycastTarget = false;
                RectTransform trackRect = trackGO.GetComponent<RectTransform>();
                trackRect.anchorMin = new Vector2(0.23f, 0.15f);
                trackRect.anchorMax = new Vector2(1f, 0.85f);
                trackRect.offsetMin = trackRect.offsetMax = Vector2.zero;

                // Fill (child of track, anchored to left — width driven by fill value)
                GameObject fillGO = CreateChild(trackGO, $"Fill_{labels[i]}");
                Image fill = fillGO.AddComponent<Image>();
                fill.color = barFillColor;
                fill.raycastTarget = false;
                RectTransform fillRect = fillGO.GetComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0f, 1f);  // width = 0, updated per weapon
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;

                fills[i] = fill;
            }

            _barFillDmg = fills[0];
            _barFillRpm = fills[1];
            _barFillMag = fills[2];
            _barFillRec = fills[3];
            _barFillVel = fills[4];
        }

        /// <summary>
        /// Updates Block B text and bar fills from <paramref name="data"/>.
        /// Called atomically on equip — <see cref="WeaponData"/> is immutable after
        /// generation so no per-frame refresh is needed.
        ///
        /// <para><b>Stat-bar normalization formulas</b>
        /// (weapon-generation.md §Stat-Bar Normalization):</para>
        /// <list type="bullet">
        ///   <item>DMG: baseDamage / 58f  (Legendary max)</item>
        ///   <item>RPM: roundsPerMinute / 330f  (Legendary max)</item>
        ///   <item>MAG: magCapacity / 20f  (Legendary max)</item>
        ///   <item>REC: (maxPitch - recoilPitch) / (maxPitch - minPitch)
        ///              = (6.5 - pitch) / (6.5 - 2.5)  — inverted: less pitch → fuller bar</item>
        ///   <item>VEL: bulletSpeed / 230f  (Legendary max)</item>
        /// </list>
        /// All results are clamped to [0, 1] before applying.
        /// </summary>
        private void RefreshWeaponPanel(WeaponData data)
        {
            if (data == null) return;

            // Rarity name + color
            if (_rarityNameText != null)
            {
                _rarityNameText.text = data.Rarity.ToString();
                _rarityNameText.color = GetRarityColor(data.Rarity);
            }

            // Bar fills — clamped to [0, 1]
            SetBarFill(_barFillDmg, Mathf.Clamp01(data.BaseDamage      / 58f));
            SetBarFill(_barFillRpm, Mathf.Clamp01(data.RoundsPerMinute / 330f));
            SetBarFill(_barFillMag, Mathf.Clamp01(data.MagCapacity     / 20f));

            // REC is inverse: low pitch (less recoil) = fuller bar
            // Normalization: (6.5 - pitch) / (6.5 - 2.5) = (6.5 - pitch) / 4.0
            float recFill = Mathf.Clamp01((6.5f - data.RecoilPitchBase) / 4.0f);
            SetBarFill(_barFillRec, recFill);

            SetBarFill(_barFillVel, Mathf.Clamp01(data.BulletSpeed / 230f));
        }

        /// <summary>
        /// Sets the fill Image's right anchor to <paramref name="fillAmount"/> [0, 1],
        /// producing a left-aligned horizontal fill bar without Image.fillAmount
        /// (which requires a Source Image and sliced sprite).
        /// </summary>
        private static void SetBarFill(Image fill, float fillAmount)
        {
            if (fill == null) return;
            RectTransform r = fill.GetComponent<RectTransform>();
            r.anchorMax = new Vector2(Mathf.Clamp01(fillAmount), 1f);
        }

        /// <summary>
        /// Returns the HUD rarity label color per ui-hud-system.md §Rarity Colors.
        /// Basic: white; Rare: sky-blue; Epic: purple; Legendary: gold.
        /// </summary>
        internal static Color GetRarityColor(WeaponRarity rarity)
        {
            return rarity switch
            {
                WeaponRarity.Basic     => new Color(0.91f, 0.91f, 0.82f, 1f),  // warm off-white
                WeaponRarity.Rare      => new Color(0.29f, 0.56f, 0.89f, 1f),  // sky blue
                WeaponRarity.Epic      => new Color(0.64f, 0.20f, 0.93f, 1f),  // purple
                WeaponRarity.Legendary => new Color(1.00f, 0.75f, 0.00f, 1f),  // gold
                _                      => Color.white
            };
        }

        // ===================================================================
        // Build — pause screen (floating at gaze-center, same style as death)
        // ===================================================================

        private void BuildPauseScreen()
        {
            if (_playerRig == null) return;
            Transform headTransform = null;

            var cam = _playerRig.GetComponentInChildren<Camera>();
            if (cam != null) headTransform = cam.transform;
            if (headTransform == null) return;

            _pauseScreenRoot = new GameObject("PauseScreen_HUD");
            _pauseScreenRoot.transform.SetParent(headTransform, false);
            _pauseScreenRoot.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            _pauseScreenRoot.transform.localRotation = Quaternion.identity;

            Canvas canvas = _pauseScreenRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            float panelW = 0.60f, panelH = 0.40f;
            float canvasW = 800f, canvasH = 540f;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(canvasW, canvasH);
            canvasRect.localScale = new Vector3(panelW / canvasW, panelH / canvasH, 1f);

            AddBgPanel(_pauseScreenRoot);

            // Title
            AddText(_pauseScreenRoot, "PauseTitle", "PAUSED", 96, _textColor,
                TextAnchor.MiddleCenter, 0f, 0.60f, 1f, 0.95f, _padding, 0f, -_padding, 0f);

            // Button prompts — vertically stacked, clear labels
            int promptSize = 56;
            Color promptColor = new Color(1f, 1f, 0.95f, 1f);

            AddText(_pauseScreenRoot, "ResumePrompt", "Trigger  —  RESUME", promptSize, promptColor,
                TextAnchor.MiddleCenter, 0.05f, 0.38f, 0.95f, 0.58f, 0f, 0f, 0f, 0f);

            AddText(_pauseScreenRoot, "RestartPrompt", "A / X  —  RESTART", promptSize, promptColor,
                TextAnchor.MiddleCenter, 0.05f, 0.18f, 0.95f, 0.38f, 0f, 0f, 0f, 0f);

            AddText(_pauseScreenRoot, "QuitPrompt", "B / Y  —  QUIT", promptSize, new Color(0.9f, 0.4f, 0.4f, 1f),
                TextAnchor.MiddleCenter, 0.05f, 0.02f, 0.95f, 0.18f, 0f, 0f, 0f, 0f);

            _pauseScreenRoot.SetActive(false);
        }

        // ===================================================================
        // Build — death screen (floating at gaze-center)
        // ===================================================================

        private void BuildDeathScreen()
        {
            if (_playerRig == null) return;
            Transform headTransform = null;

            var cam = _playerRig.GetComponentInChildren<Camera>();
            if (cam != null) headTransform = cam.transform;
            if (headTransform == null) return;

            _deathScreenRoot = new GameObject("DeathScreen_HUD");
            _deathScreenRoot.transform.SetParent(headTransform, false);
            _deathScreenRoot.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            _deathScreenRoot.transform.localRotation = Quaternion.identity;

            Canvas canvas = _deathScreenRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            float panelW = 0.60f, panelH = 0.40f;
            float canvasW = 800f, canvasH = 540f;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(canvasW, canvasH);
            canvasRect.localScale = new Vector3(panelW / canvasW, panelH / canvasH, 1f);

            AddBgPanel(_deathScreenRoot);

            // Title
            AddText(_deathScreenRoot, "DeathTitle", "YOU DIED", 96, new Color(0.9f, 0.2f, 0.2f, 1f),
                TextAnchor.MiddleCenter, 0f, 0.55f, 1f, 0.95f, _padding, 0f, -_padding, 0f);

            // Button prompts — vertically stacked, clear labels
            int promptSize = 56;
            Color promptColor = new Color(1f, 1f, 0.95f, 1f);

            AddText(_deathScreenRoot, "RestartPrompt", "Trigger  —  RESTART", promptSize, promptColor,
                TextAnchor.MiddleCenter, 0.05f, 0.22f, 0.95f, 0.45f, 0f, 0f, 0f, 0f);

            AddText(_deathScreenRoot, "QuitPrompt", "A / X  —  QUIT", promptSize, new Color(0.9f, 0.4f, 0.4f, 1f),
                TextAnchor.MiddleCenter, 0.05f, 0.02f, 0.95f, 0.22f, 0f, 0f, 0f, 0f);

            _deathScreenRoot.SetActive(false);
        }

        // ===================================================================
        // Sync
        // ===================================================================

        private void SyncToCurrentState()
        {
            if (_stateReader != null)
            {
                OnHealthChanged(_stateReader.CurrentHealth);
                OnCurrencyChanged(_stateReader.CurrentCurrency);
                OnStateChanged(_stateReader.CurrentState);
            }

            if (_weaponInstance != null)
            {
                bool isHeld = _weaponInstance.CurrentState == WeaponInstanceState.Held ||
                              _weaponInstance.CurrentState == WeaponInstanceState.Firing ||
                              _weaponInstance.CurrentState == WeaponInstanceState.Reloading ||
                              _weaponInstance.CurrentState == WeaponInstanceState.SlideBack;
                OnEquipChanged(isHeld);
            }
            else if (_ammoText != null)
            {
                _ammoText.gameObject.SetActive(false);
            }
        }

        // ===================================================================
        // UI helpers
        // ===================================================================

        private static GameObject CreateChild(GameObject parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void StretchFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void AddBgPanel(GameObject parent)
        {
            GameObject bgGO = CreateChild(parent, "BG");
            Image bg = bgGO.AddComponent<Image>();
            Color c = _bgColor;
            c.a = _bgOpacity;
            bg.color = c;
            bg.raycastTarget = false;
            StretchFill(bgGO.GetComponent<RectTransform>());
        }

        private Text AddText(GameObject parent, string name, string content,
            int fontSize, Color color, TextAnchor alignment,
            float aMinX, float aMinY, float aMaxX, float aMaxY,
            float oMinX, float oMinY, float oMaxX, float oMaxY)
        {
            GameObject go = CreateChild(parent, name);
            Text t = go.AddComponent<Text>();
            t.text = content;
            t.font = _hudFont;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(aMinX, aMinY);
            r.anchorMax = new Vector2(aMaxX, aMaxY);
            r.offsetMin = new Vector2(oMinX, oMinY);
            r.offsetMax = new Vector2(oMaxX, oMaxY);
            return t;
        }

        // ===================================================================
        // Validation
        // ===================================================================

        private void ValidateReferences()
        {
            if (_config == null)
                Debug.LogWarning("[HUDSystem] No HUDConfig assigned — using default values.", this);
            if (_playerStateManager == null)
                Debug.LogError("[HUDSystem] PlayerStateManager is not assigned.", this);
        }

        // ===================================================================
        // Test seam
        // ===================================================================

        internal string TestGetHealthText() => _healthText != null ? _healthText.text : null;
        internal string TestGetCurrencyText() => _currencyText != null ? _currencyText.text : null;
        internal string TestGetAmmoText() => _ammoText != null ? _ammoText.text : null;
        internal bool TestIsHandDisplayActive() => _handDisplayRoot != null && _handDisplayRoot.activeSelf;
        internal bool TestIsPausePanelActive() => _pausePanel != null && _pausePanel.activeSelf;
        internal bool TestIsPauseScreenActive() => _pauseScreenRoot != null && _pauseScreenRoot.activeSelf;
        internal bool TestIsDeathScreenActive() => _deathScreenRoot != null && _deathScreenRoot.activeSelf;

        internal int TestGetFilledSegmentCount()
        {
            if (_healthSegments == null) return -1;
            int count = 0;
            for (int i = 0; i < _healthSegments.Length; i++)
                if (_healthSegments[i] != null && _healthSegments[i].color == _segmentFilledColor)
                    count++;
            return count;
        }

        internal void InjectDependencies(
            IPlayerStateReader stateReader,
            IRigControllerProvider rigControllerProvider)
        {
            _stateReader = stateReader;
            _rigControllerProvider = rigControllerProvider;

            // Test seam: if Awake() ran before injection (typical in tests where
            // AddComponent<HUDSystem>() triggers Awake before InjectDependencies),
            // build the UI now that dependencies are available. Production path
            // already built during Awake — null guards short-circuit the rebuild.
            if (_rigControllerProvider != null && _handDisplayRoot == null)
            {
                BuildHandDisplay();
                BuildWeaponPanel();
            }
        }

        // ===================================================================
        // HUD-06 test seams
        // ===================================================================

        /// <summary>True if the Block B weapon panel root exists and is active.</summary>
        internal bool TestIsWeaponBlockBActive() =>
            _weaponBlockB != null && _weaponBlockB.activeSelf;

        /// <summary>Current text of the rarity name label. Null if panel not built.</summary>
        internal string TestGetRarityNameText() => _rarityNameText?.text;

        /// <summary>Current color of the rarity name label.</summary>
        internal Color TestGetRarityNameColor() =>
            _rarityNameText != null ? _rarityNameText.color : Color.clear;

        /// <summary>Current right-anchor X of the DMG fill bar [0, 1].</summary>
        internal float TestGetBarFillDmg() => GetBarFillValue(_barFillDmg);

        /// <summary>Current right-anchor X of the RPM fill bar [0, 1].</summary>
        internal float TestGetBarFillRpm() => GetBarFillValue(_barFillRpm);

        /// <summary>Current right-anchor X of the MAG fill bar [0, 1].</summary>
        internal float TestGetBarFillMag() => GetBarFillValue(_barFillMag);

        /// <summary>Current right-anchor X of the REC fill bar [0, 1].</summary>
        internal float TestGetBarFillRec() => GetBarFillValue(_barFillRec);

        /// <summary>Current right-anchor X of the VEL fill bar [0, 1].</summary>
        internal float TestGetBarFillVel() => GetBarFillValue(_barFillVel);

        /// <summary>True if a spread UI bar was created as a named child of Block B.</summary>
        internal bool TestSpreadBarExists()
        {
            if (_weaponBlockB == null) return false;
            // Spread is intentionally NOT exposed as a bar in HUD-06
            return _weaponBlockB.GetComponentsInChildren<UnityEngine.UI.Image>(true) is var arr
                   && System.Array.Exists(arr, img => img != null && img.name.Contains("Spread"));
        }

        private static float GetBarFillValue(Image fill)
        {
            if (fill == null) return -1f;
            return fill.GetComponent<RectTransform>().anchorMax.x;
        }

        /// <summary>
        /// Drives <see cref="RefreshWeaponPanel"/> directly from a test-supplied
        /// <see cref="WeaponData"/> without needing a live <see cref="WeaponInstance"/>.
        /// Also forces Block B visibility to match <paramref name="isEquipped"/>.
        /// </summary>
        internal void TestSimulateEquipChanged(bool isEquipped, WeaponData data = null)
        {
            if (_weaponBlockB != null)
                _weaponBlockB.SetActive(isEquipped);
            if (_ammoText != null)
                _ammoText.gameObject.SetActive(isEquipped);
            if (isEquipped && data != null)
                RefreshWeaponPanel(data);
        }
    }
}
