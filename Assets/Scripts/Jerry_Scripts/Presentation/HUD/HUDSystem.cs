using JerryScripts.Core.PlayerState;
using JerryScripts.Foundation.Damage;
using JerryScripts.Foundation.Player;
using TMPro;
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
    /// Running state. Refreshed atomically on <see cref="HUDWeaponBus.OnEquipChanged"/>.
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

        [Header("BNG Rig Wiring")]
        [Tooltip("Drag the BNG rig's Left Controller Transform here. Block A (running + pause) and Block B " +
                 "(HUD-06) mount to this. Required — HUD will not build without it.")]
        [SerializeField] private Transform _leftControllerOverride;

        [Tooltip("Drag the BNG rig's Camera / head Transform here. Pause and Death floating screens parent " +
                 "to this so they appear at gaze-center. Required — pause/death screens skipped without it.")]
        [SerializeField] private Transform _headTransform;

        [Header("Menu Input — Pause Toggle (used during Running / Paused)")]
        [Tooltip("Pressing this in Running state pauses; in Paused state, unpauses (resumes).")]
        [SerializeField] private InputActionReference _pauseToggleAction;

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

        // HUD subscribes to HUDWeaponBus static events instead of holding a bridge reference.
        // The bridge (BNGWeaponBridge) lives in default Assembly-CSharp and publishes here.

        // ===================================================================
        // UI — hand display (shared canvas for Running + Paused)
        // ===================================================================

        private GameObject _handDisplayRoot;
        private GameObject _runningPanel;
        private Image[] _healthSegments;
        private TextMeshProUGUI _healthText;
        private TextMeshProUGUI _currencyText;
        private TextMeshProUGUI _ammoText;
        private GameObject _pausePanel;

        // ===================================================================
        // UI — weapon panel (HUD-06, Block B)
        // ===================================================================

        /// <summary>Root canvas GameObject for Block B. Hidden by default.</summary>
        private GameObject _weaponBlockB;
        private TextMeshProUGUI _rarityNameText;
        private RawImage _pistolSilhouetteImage;
        // HUD-06 stat bars — 5 bars × 10 segments each (matches health-bar style)
        private const int StatBarSegmentCount = 10;
        private Image[] _statBarSegmentsDmg;
        private Image[] _statBarSegmentsRpm;
        private Image[] _statBarSegmentsMag;
        private Image[] _statBarSegmentsRec;
        private Image[] _statBarSegmentsVel;

        // HUD-06 stat value text (right of each bar)
        private TextMeshProUGUI _statValueDmg;
        private TextMeshProUGUI _statValueRpm;
        private TextMeshProUGUI _statValueMag;
        private TextMeshProUGUI _statValueRec;
        private TextMeshProUGUI _statValueVel;

        // Cached normalized fill values [0, 1] per bar — used by test seams.
        private float _statFillDmg;
        private float _statFillRpm;
        private float _statFillMag;
        private float _statFillRec;
        private float _statFillVel;

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

            HUDWeaponBus.OnAmmoChanged  += OnAmmoChanged;
            HUDWeaponBus.OnEquipChanged += OnBusEquipChanged;
            HUDWeaponBus.OnStatsChanged += OnBusStatsChanged;

            // Replay last-known state in case the bridge published before HUDSystem was enabled
            if (HUDWeaponBus.LastAmmoMax > 0)
                OnAmmoChanged(HUDWeaponBus.LastAmmoCurrent, HUDWeaponBus.LastAmmoMax);
            OnBusEquipChanged(HUDWeaponBus.LastIsHeld);
            if (HUDWeaponBus.HasPublishedStats)
                OnBusStatsChanged(HUDWeaponBus.LastStats);

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

            HUDWeaponBus.OnAmmoChanged  -= OnAmmoChanged;
            HUDWeaponBus.OnEquipChanged -= OnBusEquipChanged;
            HUDWeaponBus.OnStatsChanged -= OnBusStatsChanged;

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
            SubscribeAction(_pauseToggleAction, OnMenuPauseToggle);
        }

        private void UnsubscribeMenuInput()
        {
            UnsubscribeAction(_leftTriggerAction, OnMenuTrigger);
            UnsubscribeAction(_leftPrimaryAction, OnMenuPrimary);
            UnsubscribeAction(_leftSecondaryAction, OnMenuSecondary);
            UnsubscribeAction(_rightTriggerAction, OnMenuTrigger);
            UnsubscribeAction(_rightPrimaryAction, OnMenuPrimary);
            UnsubscribeAction(_rightSecondaryAction, OnMenuSecondary);
            UnsubscribeAction(_pauseToggleAction, OnMenuPauseToggle);
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

        private void OnMenuPauseToggle(InputAction.CallbackContext _)
        {
            if (IsMenuInputBlocked()) return;

            switch (_currentDisplayState)
            {
                case PlayerState.Running:
                    _stateWriter?.RequestPause();
                    break;
                case PlayerState.Paused:
                    _stateWriter?.RequestResume();
                    break;
                case PlayerState.Dead:
                default:
                    // Ignored — death screen has its own restart/quit input.
                    break;
            }
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
                // Continuous fractional fill — each segment can be any value between
                // empty and full. Boundary segment is color-lerped by its fractional part.
                float segmentsFilled = fillRatio * HealthSegmentCount;
                int wholeFilled = Mathf.FloorToInt(segmentsFilled);
                float fractional = segmentsFilled - wholeFilled;

                for (int i = 0; i < _healthSegments.Length; i++)
                {
                    if (_healthSegments[i] == null) continue;

                    if (i < wholeFilled)
                    {
                        // Fully filled
                        _healthSegments[i].color = _segmentFilledColor;
                    }
                    else if (i == wholeFilled && fractional > 0f)
                    {
                        // Boundary segment — lerp colors by fractional fill
                        _healthSegments[i].color = Color.Lerp(
                            _segmentEmptyColor, _segmentFilledColor, fractional);
                    }
                    else
                    {
                        // Fully empty
                        _healthSegments[i].color = _segmentEmptyColor;
                    }
                }
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

        private void OnBusEquipChanged(bool isEquipped)
        {
            // Block A ammo text
            if (_ammoText != null)
            {
                _ammoText.gameObject.SetActive(isEquipped);
            }

            // Block B — weapon stat panel (HUD-06)
            if (_weaponBlockB != null)
                _weaponBlockB.SetActive(isEquipped);

            if (isEquipped && HUDWeaponBus.HasPublishedStats)
                RefreshWeaponPanel(HUDWeaponBus.LastStats);
        }

        private void OnBusStatsChanged(WeaponStatsSnapshot snapshot)
        {
            // Always refresh — keeps the panel in sync if stats change after equip (rare)
            RefreshWeaponPanel(snapshot);
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

            _fontSize = 52;
            _padding = 16f;
        }

        // ===================================================================
        // Build — hand display (Running + Pause panels on same canvas)
        // ===================================================================

        private void BuildHandDisplay()
        {
            if (_leftControllerOverride == null) return;
            Transform leftController = _leftControllerOverride;

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

            AddText(parent, "HealthLabel", "♥", _fontSize, barFillColor, TextAlignmentOptions.Center,
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
                TextAlignmentOptions.Center, 0.80f, 0.5f, 1f, 1f, 0f, _padding, -_padding, -_padding);

            _currencyText = AddText(parent, "CurrencyText", "$ 0", _fontSize, _textColor,
                TextAlignmentOptions.MidlineLeft, 0f, 0f, 0.5f, 0.5f, _padding * 2.5f, _padding, -_padding, -_padding);

            _ammoText = AddText(parent, "AmmoText", "-- / --", _fontSize, _textColor,
                TextAlignmentOptions.MidlineRight, 0.5f, 0f, 1f, 0.5f, _padding, _padding, -_padding * 2f, -_padding);
        }

        private void BuildPauseContent(GameObject parent)
        {
            // Title — large, centered at top
            AddText(parent, "PauseTitle", "PAUSED", _fontSize + 4, _textColor,
                TextAlignmentOptions.Center, 0f, 0.55f, 1f, 1f, _padding, 0f, -_padding, -_padding);

            // Button prompts — stacked vertically with readable text
            int promptSize = _fontSize - 4;
            Color promptColor = new Color(1f, 1f, 0.95f, 1f);

            AddText(parent, "ResumePrompt", "Trigger  RESUME", promptSize, promptColor,
                TextAlignmentOptions.Center, 0.05f, 0.28f, 0.95f, 0.52f, 0f, 0f, 0f, 0f);

            AddText(parent, "RestartPrompt", "A / X  RESTART", promptSize, promptColor,
                TextAlignmentOptions.Center, 0.05f, 0.02f, 0.55f, 0.26f, 0f, 0f, 0f, 0f);

            AddText(parent, "QuitPrompt", "B / Y  QUIT", promptSize, new Color(0.9f, 0.4f, 0.4f, 1f),
                TextAlignmentOptions.Center, 0.55f, 0.02f, 0.95f, 0.26f, 0f, 0f, 0f, 0f);
        }

        // ===================================================================
        // Build — weapon stat panel (HUD-06, Block B, S2-009)
        // ===================================================================

        /// <summary>
        /// Builds the HUD-06 Block B canvas at the Inspector-configured offset/rotation.
        /// Layout:
        /// <list type="number">
        ///   <item>Left column (~30%): pistol silhouette icon (larger than Block A elements)</item>
        ///   <item>Top of left column: rarity name text</item>
        ///   <item>Right column (~70%): five segmented stat bars (DMG/RPM/MAG/REC/VEL)
        ///     each with a numeric value displayed to the right.</item>
        /// </list>
        /// Bar segments mirror the health-bar style: 10 white-on-dark segments toggled by
        /// the normalized fill formulas in weapon-generation.md §Stat-Bar Normalization.
        /// </summary>
        private void BuildWeaponPanel()
        {
            if (_leftControllerOverride == null) return;
            Transform leftController = _leftControllerOverride;

            float panelWidth   = _config != null ? _config.PanelWidth         : 0.12f;
            float blockBHeight = _config != null ? _config.WeaponPanelHeight  : 0.06f;

            Vector3 blockBOffset   = _config != null ? _config.WeaponPanelLocalOffset   : new Vector3(0f, 0.02f, -0.05f);
            Vector3 blockBRotation = _config != null ? _config.WeaponPanelLocalRotation : new Vector3(-30f, 0f, 0f);

            _weaponBlockB = new GameObject("WeaponPanel_HUD");
            _weaponBlockB.transform.SetParent(leftController, false);
            _weaponBlockB.transform.localPosition = blockBOffset;
            _weaponBlockB.transform.localRotation = Quaternion.Euler(blockBRotation);

            Canvas canvas = _weaponBlockB.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            float canvasW = 800f, canvasH = 360f;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(canvasW, canvasH);
            canvasRect.localScale = new Vector3(panelWidth / canvasW, blockBHeight / canvasH, 1f);

            AddBgPanel(_weaponBlockB);

            // --- Left column: rarity name (top) + larger silhouette icon (below) ---

            _rarityNameText = AddText(
                _weaponBlockB, "RarityName", "Basic",
                _fontSize - 4, Color.white, TextAlignmentOptions.Center,
                0f, 0.78f, 0.30f, 1f,
                _padding, 0f, -_padding, -_padding);

            // Larger silhouette: 30% width × 75% height (was 25% × 52%)
            GameObject iconCell = CreateChild(_weaponBlockB, "PistolIcon");
            RectTransform iconCellRect = iconCell.GetComponent<RectTransform>();
            iconCellRect.anchorMin = new Vector2(0f, 0f);
            iconCellRect.anchorMax = new Vector2(0.30f, 0.75f);
            iconCellRect.offsetMin = new Vector2(_padding, _padding);
            iconCellRect.offsetMax = new Vector2(-_padding, -_padding);

            GameObject iconGO = CreateChild(iconCell, "Icon");
            RawImage iconImage = iconGO.AddComponent<RawImage>();
            iconImage.raycastTarget = false;

            Texture2D silhouette = _config != null ? _config.PistolSilhouette : null;
            if (silhouette != null)
            {
                iconImage.texture = silhouette;
                var fitter = iconGO.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)silhouette.width / Mathf.Max(1, silhouette.height);
            }
            else
            {
                Debug.LogWarning(
                    "[HUDSystem] HUDConfig.PistolSilhouette is not assigned. " +
                    "The HUD-06 icon cell will be empty. Assign a pistol silhouette Texture2D " +
                    "(any default-imported PNG works) in the HUDConfig asset. (ui-hud-system.md Rule 19)",
                    this);
                iconImage.color = new Color(0f, 0f, 0f, 0f);
            }
            _pistolSilhouetteImage = iconImage;

            // --- Right column: five segmented stat bars with numeric values ---
            BuildStatBars(_weaponBlockB);

            _weaponBlockB.SetActive(false);
        }

        /// <summary>
        /// Builds five rows in the right ~68% of Block B. Each row contains:
        /// label (left) + 10 segmented bar Images (middle) + numeric value Text (right).
        /// Segment style mirrors the health bar: 10 white segments with small gaps.
        /// Filled segments use _segmentFilledColor, empty use _segmentEmptyColor.
        /// </summary>
        private void BuildStatBars(GameObject parent)
        {
            // Bar container: right 68% of the canvas
            GameObject barsRoot = CreateChild(parent, "StatBars");
            RectTransform barsRect = barsRoot.GetComponent<RectTransform>();
            barsRect.anchorMin = new Vector2(0.31f, 0f);
            barsRect.anchorMax = new Vector2(1f, 1f);
            barsRect.offsetMin = new Vector2(_padding * 0.5f, _padding);
            barsRect.offsetMax = new Vector2(-_padding, -_padding);

            string[] labels = { "DMG", "RPM", "MAG", "REC", "VEL" };
            float rowH = 1f / labels.Length;
            int labelSize = _fontSize - 14;

            Image[][] allSegments = new Image[labels.Length][];
            TextMeshProUGUI[] allValues = new TextMeshProUGUI[labels.Length];

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

                // Label (left ~18%)
                AddText(row, $"Label_{labels[i]}", labels[i],
                    labelSize, _textColor, TextAlignmentOptions.MidlineLeft,
                    0f, 0f, 0.18f, 1f,
                    0f, 0f, 0f, 0f);

                // Segments container (middle ~62%)
                GameObject segContainer = CreateChild(row, $"Segments_{labels[i]}");
                RectTransform segRect = segContainer.GetComponent<RectTransform>();
                segRect.anchorMin = new Vector2(0.19f, 0.18f);
                segRect.anchorMax = new Vector2(0.80f, 0.82f);
                segRect.offsetMin = segRect.offsetMax = Vector2.zero;

                // Build 10 segments matching the health-bar style
                Image[] segs = new Image[StatBarSegmentCount];
                float gapFraction = 0.015f;
                float segWidth = (1f - gapFraction * (StatBarSegmentCount - 1)) / StatBarSegmentCount;
                for (int s = 0; s < StatBarSegmentCount; s++)
                {
                    float xMin = s * (segWidth + gapFraction);
                    GameObject segGO = CreateChild(segContainer, $"Seg_{s}");
                    Image segImg = segGO.AddComponent<Image>();
                    segImg.color = _segmentEmptyColor;
                    segImg.raycastTarget = false;
                    segs[s] = segImg;
                    RectTransform sr = segGO.GetComponent<RectTransform>();
                    sr.anchorMin = new Vector2(xMin, 0f);
                    sr.anchorMax = new Vector2(xMin + segWidth, 1f);
                    sr.offsetMin = sr.offsetMax = Vector2.zero;
                }
                allSegments[i] = segs;

                // Numeric value text (right ~18%)
                TextMeshProUGUI valueText = AddText(row, $"Value_{labels[i]}", "--",
                    labelSize, _textColor, TextAlignmentOptions.MidlineRight,
                    0.81f, 0f, 1f, 1f,
                    0f, 0f, 0f, 0f);
                allValues[i] = valueText;
            }

            _statBarSegmentsDmg = allSegments[0];
            _statBarSegmentsRpm = allSegments[1];
            _statBarSegmentsMag = allSegments[2];
            _statBarSegmentsRec = allSegments[3];
            _statBarSegmentsVel = allSegments[4];

            _statValueDmg = allValues[0];
            _statValueRpm = allValues[1];
            _statValueMag = allValues[2];
            _statValueRec = allValues[3];
            _statValueVel = allValues[4];
        }

        /// <summary>
        /// Updates Block B text and bar fills from the <paramref name="snapshot"/>'s generated
        /// weapon stats. Called atomically on equip; data is set once at weapon Start.
        ///
        /// <para><b>Stat-bar normalization</b> (sprint-final.md §Stat Mapping):</para>
        /// <list type="bullet">
        ///   <item>DMG: DamageScale / 2.0  (Sam may currently hardcode to 1.0 → bar at 50%)</item>
        ///   <item>RPM: hidden — BNG doesn't expose shot interval; bar shows 0 with text "—"</item>
        ///   <item>MAG: MagazineSize / 30  (Sam may currently hardcode to 0 → bar empty)</item>
        ///   <item>REC: (1.5 - RecoilIntensityScale) / 1.0  — lower scale = lower recoil = fuller bar</item>
        ///   <item>VEL: (ProjectileVelocityScale - 0.85) / 0.5</item>
        /// </list>
        ///
        /// <para>Rarity color: MaxRarityRoll → 0=Basic, 1=Rare, 2=Epic.</para>
        /// </summary>
        private void RefreshWeaponPanel(WeaponStatsSnapshot snapshot)
        {
            // Map MaxRarityRoll → WeaponRarity for color
            WeaponRarity rarity = MapToWeaponRarity(snapshot.MaxRarityRoll);

            if (_rarityNameText != null)
            {
                _rarityNameText.text = rarity.ToString();
                _rarityNameText.color = GetRarityColor(rarity);
            }

            // Cache normalized fill values (used by tests)
            // Bar formulas use generic 0–2.0 scale ranges (or 0–30 for MAG). Tune later
            // if Sam's actual stat ranges differ. The previous formulas (e.g. VEL clamped
            // to (0.85, 1.35)) emptied the bar when scales fell outside that narrow window
            // — values like 0.5 displayed as "50" but had a 0% bar.
            _statFillDmg = Mathf.Clamp01(snapshot.DamageScale / 2.0f);
            _statFillRpm = 0f; // BNG does not expose shot interval — bar empty
            _statFillMag = snapshot.MagazineSize > 0 ? Mathf.Clamp01(snapshot.MagazineSize / 30f) : 0f;
            // REC: lower scale = lower recoil = fuller bar. (2.0 - scale) / 2.0 clamped.
            _statFillRec = Mathf.Clamp01((2.0f - snapshot.RecoilIntensityScale) / 2.0f);
            _statFillVel = Mathf.Clamp01(snapshot.ProjectileVelocityScale / 2.0f);

            ApplySegmentFill(_statBarSegmentsDmg, _statFillDmg);
            ApplySegmentFill(_statBarSegmentsRpm, _statFillRpm);
            ApplySegmentFill(_statBarSegmentsMag, _statFillMag);
            ApplySegmentFill(_statBarSegmentsRec, _statFillRec);
            ApplySegmentFill(_statBarSegmentsVel, _statFillVel);

            if (_statValueDmg != null) _statValueDmg.text = (snapshot.DamageScale * 100f).ToString("F0");
            if (_statValueRpm != null) _statValueRpm.text = "—";
            if (_statValueMag != null) _statValueMag.text = snapshot.MagazineSize.ToString();
            if (_statValueRec != null) _statValueRec.text = snapshot.RecoilIntensityScale.ToString("F2");
            if (_statValueVel != null) _statValueVel.text = (snapshot.ProjectileVelocityScale * 100f).ToString("F0");
        }

        private static WeaponRarity MapToWeaponRarity(int maxRarityRoll)
        {
            return maxRarityRoll switch
            {
                0 => WeaponRarity.Basic,
                1 => WeaponRarity.Rare,
                2 => WeaponRarity.Epic,
                _ => WeaponRarity.Legendary  // fallback; Sam tops at 2 today
            };
        }

        /// <summary>
        /// Toggles segments in <paramref name="segments"/> between filled and empty colors
        /// based on <paramref name="fillAmount"/> [0, 1]. Mirrors the health-bar pattern:
        /// filled count = ceil(fillAmount * segmentCount).
        /// </summary>
        private void ApplySegmentFill(Image[] segments, float fillAmount)
        {
            if (segments == null) return;
            int filled = Mathf.CeilToInt(Mathf.Clamp01(fillAmount) * segments.Length);
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null)
                    segments[i].color = i < filled ? _segmentFilledColor : _segmentEmptyColor;
            }
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

        /// <summary>
        /// Resolves the parent transform for floating gaze-center panels (Pause / Death).
        /// Prefers <see cref="Camera.main"/> (always at eye level in VR rigs) over the
        /// Inspector-wired <c>_headTransform</c>. Returns null if neither resolves.
        /// </summary>
        private Transform ResolveGazeCenterAnchor()
        {
            if (Camera.main != null) return Camera.main.transform;
            return _headTransform;
        }

        private void BuildPauseScreen()
        {
            Transform headTransform = ResolveGazeCenterAnchor();
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
                TextAlignmentOptions.Center, 0f, 0.60f, 1f, 0.95f, _padding, 0f, -_padding, 0f);

            // Button prompts — vertically stacked, clear labels
            int promptSize = 56;
            Color promptColor = new Color(1f, 1f, 0.95f, 1f);

            AddText(_pauseScreenRoot, "ResumePrompt", "Trigger  —  RESUME", promptSize, promptColor,
                TextAlignmentOptions.Center, 0.05f, 0.38f, 0.95f, 0.58f, 0f, 0f, 0f, 0f);

            AddText(_pauseScreenRoot, "RestartPrompt", "A / X  —  RESTART", promptSize, promptColor,
                TextAlignmentOptions.Center, 0.05f, 0.18f, 0.95f, 0.38f, 0f, 0f, 0f, 0f);

            AddText(_pauseScreenRoot, "QuitPrompt", "B / Y  —  QUIT", promptSize, new Color(0.9f, 0.4f, 0.4f, 1f),
                TextAlignmentOptions.Center, 0.05f, 0.02f, 0.95f, 0.18f, 0f, 0f, 0f, 0f);

            _pauseScreenRoot.SetActive(false);
        }

        // ===================================================================
        // Build — death screen (floating at gaze-center)
        // ===================================================================

        private void BuildDeathScreen()
        {
            Transform headTransform = ResolveGazeCenterAnchor();
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
                TextAlignmentOptions.Center, 0f, 0.55f, 1f, 0.95f, _padding, 0f, -_padding, 0f);

            // Button prompts — vertically stacked, clear labels
            int promptSize = 56;
            Color promptColor = new Color(1f, 1f, 0.95f, 1f);

            AddText(_deathScreenRoot, "RestartPrompt", "Trigger  —  RESTART", promptSize, promptColor,
                TextAlignmentOptions.Center, 0.05f, 0.22f, 0.95f, 0.45f, 0f, 0f, 0f, 0f);

            AddText(_deathScreenRoot, "QuitPrompt", "A / X  —  QUIT", promptSize, new Color(0.9f, 0.4f, 0.4f, 1f),
                TextAlignmentOptions.Center, 0.05f, 0.02f, 0.95f, 0.22f, 0f, 0f, 0f, 0f);

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

            // Sync weapon-side state from the static bus
            OnBusEquipChanged(HUDWeaponBus.LastIsHeld);
            if (HUDWeaponBus.LastAmmoMax > 0)
                OnAmmoChanged(HUDWeaponBus.LastAmmoCurrent, HUDWeaponBus.LastAmmoMax);
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

        private TextMeshProUGUI AddText(GameObject parent, string name, string content,
            int fontSize, Color color, TextAlignmentOptions alignment,
            float aMinX, float aMinY, float aMaxX, float aMaxY,
            float oMinX, float oMinY, float oMaxX, float oMaxY)
        {
            GameObject go = CreateChild(parent, name);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
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

        /// <summary>Current normalized [0, 1] fill of the DMG bar.
        /// Returns -1 if Block B was never built (left controller override was null at Awake).</summary>
        internal float TestGetBarFillDmg() => _statBarSegmentsDmg != null ? _statFillDmg : -1f;

        /// <summary>Current normalized [0, 1] fill of the RPM bar.</summary>
        internal float TestGetBarFillRpm() => _statBarSegmentsRpm != null ? _statFillRpm : -1f;

        /// <summary>Current normalized [0, 1] fill of the MAG bar.</summary>
        internal float TestGetBarFillMag() => _statBarSegmentsMag != null ? _statFillMag : -1f;

        /// <summary>Current normalized [0, 1] fill of the REC bar.</summary>
        internal float TestGetBarFillRec() => _statBarSegmentsRec != null ? _statFillRec : -1f;

        /// <summary>Current normalized [0, 1] fill of the VEL bar.</summary>
        internal float TestGetBarFillVel() => _statBarSegmentsVel != null ? _statFillVel : -1f;

        /// <summary>True if a spread UI bar was created as a named child of Block B.</summary>
        internal bool TestSpreadBarExists()
        {
            if (_weaponBlockB == null) return false;
            // Spread is intentionally NOT exposed as a bar in HUD-06
            return _weaponBlockB.GetComponentsInChildren<UnityEngine.UI.Image>(true) is var arr
                   && System.Array.Exists(arr, img => img != null && img.name.Contains("Spread"));
        }

        /// <summary>
        /// Drives <see cref="RefreshWeaponPanel"/> directly from a test-supplied
        /// <see cref="WeaponStatsSnapshot"/> without needing a live <see cref="BNGWeaponBridge"/>.
        /// Also forces Block B visibility to match <paramref name="isEquipped"/>.
        /// </summary>
        internal void TestSimulateEquipChanged(bool isEquipped, WeaponStatsSnapshot? snapshot = null)
        {
            if (_weaponBlockB != null)
                _weaponBlockB.SetActive(isEquipped);
            if (_ammoText != null)
                _ammoText.gameObject.SetActive(isEquipped);
            if (isEquipped && snapshot.HasValue)
                RefreshWeaponPanel(snapshot.Value);
        }
    }
}
