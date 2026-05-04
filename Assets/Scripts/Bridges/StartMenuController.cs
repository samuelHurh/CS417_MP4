using JerryScripts.Foundation.Player;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tutorial start-menu controller. Drives the multi-page tutorial canvas, four
/// BNG buttons (Start, Quit, Next, Prev), and the start-button lock state.
///
/// <para><b>Lock logic</b>: the Start button is LOCKED on a fresh save until the
/// player fires their first bullet (detected via <see cref="HUDWeaponBus.OnAmmoChanged"/>
/// — when ammo decreases, a shot was fired). Pressing Start while locked is a no-op
/// with a Console log. Once the player presses Start successfully, <see cref="SaveData.TutorialCompleted"/>
/// is set to true — future sessions skip the lock entirely.</para>
///
/// <para><b>Wiring</b>: same pattern as <see cref="PurchaseStation"/> — drag the four
/// BNG Button GameObjects into the four anchor slots; on each button's
/// <c>onButtonDown</c> UnityEvent, drag this controller and select the matching
/// <c>OnXXXButtonPressed</c> method.</para>
///
/// <para><b>Tutorial pages</b>: <see cref="_pageContents"/> is an array of strings.
/// <see cref="_pageText"/> is a UI Text component on the world-space tutorial canvas
/// (author the canvas in the scene; drag its Text into the slot). Next/Prev cycle
/// through the array; the controller updates <see cref="_pageText"/>.text and the
/// optional <see cref="_pageCounterText"/> ("1 / 4").</para>
///
/// <para><b>Asmdef boundary note</b>: lives in default Assembly-CSharp because it
/// references HUDWeaponBus (which is in Jerry's asmdef — referenceable from default).</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class StartMenuController : MonoBehaviour
{
    // ============================================================================
    // Inspector — Tutorial Pages
    // ============================================================================

    [Header("Tutorial Pages")]
    [Tooltip("Text content for each page. Next/Prev buttons cycle through these. " +
             "Suggested first page content: control reference (A=slide reseat, B=mag eject, " +
             "X=pause, Trigger=fire, Grab=grab, holster rings + ammo pouch).")]
    [TextArea(3, 10)]
    [SerializeField] private string[] _pageContents = new string[]
    {
        "WELCOME\n\nLook around to find your weapon on the table.\nPick it up with the Grab button.",
        "CONTROLS\n\nTrigger — Shoot\nGrab — Pick up items\nA — Reseat barrel slide (after empty)\nB — Eject magazine\nX — Pause / Resume",
        "RELOADING\n\nLook down at your hip:\n• Two holster rings hold weapons\n• Reach between them for a fresh magazine (AMMO pouch)\n\nEject empty mag with B,\ngrab a new one,\nbring it to the gun.",
        "READY\n\nFire one shot at the table to unlock the START button.\nThen press START to begin.",
    };

    [Tooltip("UI Text component on the tutorial canvas — controller updates its .text per page.")]
    [SerializeField] private TextMeshProUGUI _pageText;

    [Tooltip("Optional UI Text showing 'N / M' page counter. Leave null to skip.")]
    [SerializeField] private TextMeshProUGUI _pageCounterText;

    // ============================================================================
    // Inspector — Buttons
    // ============================================================================

    [Header("Buttons (drag BNG Button GOs here for label anchors)")]
    [SerializeField] private Transform _startButtonAnchor;
    [SerializeField] private Transform _quitButtonAnchor;
    [SerializeField] private Transform _nextButtonAnchor;
    [SerializeField] private Transform _prevButtonAnchor;

    // ============================================================================
    // Inspector — Lock
    // ============================================================================

    [Header("Start Button Lock")]
    [Tooltip("Label text shown when Start is unlocked.")]
    [SerializeField] private string _startReadyText = "START";

    [Tooltip("Label text shown when Start is locked.")]
    [SerializeField] private string _startLockedText = "LOCKED";

    // ============================================================================
    // Inspector — Scene
    // ============================================================================

    [Header("Scene Loading")]
    [Tooltip("Name of the main game scene (in Build Settings) loaded when Start is pressed.")]
    [SerializeField] private string _gameSceneName = "Final_Scene";

    // ============================================================================
    // Inspector — Label style (matches PurchaseStation defaults)
    // ============================================================================

    [Header("Label Style")]
    [SerializeField] private Vector3 _labelLocalOffset = new Vector3(0f, 0.3f, 0f);
    [Min(0.02f)] [SerializeField] private float _labelWorldHeight = 0.15f;
    [Min(8)] [SerializeField] private int _labelFontSize = 64;
    [SerializeField] private Color _labelReadyColor = new Color(1f, 0.95f, 0.4f, 1f);
    [SerializeField] private Color _labelLockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color _labelBgColor = new Color(0.05f, 0.05f, 0.05f, 1f);
    [Range(0f, 1f)] [SerializeField] private float _labelBgOpacity = 0.65f;
    [Range(0.1f, 1.0f)] [SerializeField] private float _labelBgWidthFraction = 0.5f;
    [Range(0.1f, 1.0f)] [SerializeField] private float _labelBgHeightFraction = 1.0f;

    // ============================================================================
    // Runtime state
    // ============================================================================

    private int _currentPage;
    private bool _firedShotThisSession;
    private int _lastSeenAmmo = -1;

    private TextMeshProUGUI _startLabelText;
    private Camera _cachedCamera;

    private GameObject[] _labelRoots = new GameObject[4];
    // 0=Start, 1=Quit, 2=Next, 3=Prev — for billboard

    // ============================================================================
    // Unity lifecycle
    // ============================================================================

    private void Start()
    {
        // Defensive: scenes can inherit a frozen timeScale from the previous scene
        // (e.g. if PSM died and reloaded into the start menu).
        Time.timeScale = 1f;

        BuildButtonLabels();
        UpdatePageDisplay();
        UpdateStartLabel();
    }

    private void OnEnable()
    {
        HUDWeaponBus.OnAmmoChanged += OnAmmoChanged;
    }

    private void OnDisable()
    {
        HUDWeaponBus.OnAmmoChanged -= OnAmmoChanged;
    }

    private void LateUpdate()
    {
        if (_cachedCamera == null)
        {
            _cachedCamera = Camera.main;
            if (_cachedCamera == null) return;
        }
        for (int i = 0; i < _labelRoots.Length; i++) BillboardLabel(_labelRoots[i]);
    }

    // ============================================================================
    // Public — wired to BNG Button onButtonDown UnityEvents
    // ============================================================================

    public void OnStartButtonPressed()
    {
        // Entry log — if you don't see this, the BNG Button onButtonDown UnityEvent
        // isn't wired to this method (check the button's Inspector).
        Debug.Log("[StartMenuController] OnStartButtonPressed CALLED. " +
                  $"IsStartUnlocked={IsStartUnlocked()}, _gameSceneName='{_gameSceneName}'.", this);

        if (!IsStartUnlocked())
        {
            Debug.Log("[StartMenuController] Start is locked — fire a bullet first to unlock.", this);
            return;
        }

        // Persist tutorial completion BEFORE loading the next scene so the next
        // run skips the lock immediately.
        SaveData.TutorialCompleted = true;

        if (string.IsNullOrEmpty(_gameSceneName))
        {
            Debug.LogError("[StartMenuController] _gameSceneName is empty — cannot load game scene. " +
                           "Set this field on the StartMenuController Inspector.", this);
            return;
        }

        Debug.Log($"[StartMenuController] Loading scene '{_gameSceneName}' now. " +
                  "If nothing happens after this, the scene is not in Build Settings " +
                  "(File > Build Settings > Scenes In Build).", this);

        SceneManager.LoadScene(_gameSceneName);
    }

    public void OnQuitButtonPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnNextPageButtonPressed()
    {
        if (_pageContents == null || _pageContents.Length == 0) return;
        _currentPage = (_currentPage + 1) % _pageContents.Length;
        UpdatePageDisplay();
    }

    public void OnPrevPageButtonPressed()
    {
        if (_pageContents == null || _pageContents.Length == 0) return;
        _currentPage = (_currentPage - 1 + _pageContents.Length) % _pageContents.Length;
        UpdatePageDisplay();
    }

    // ============================================================================
    // Lock state
    // ============================================================================

    private bool IsStartUnlocked()
    {
        return SaveData.TutorialCompleted || _firedShotThisSession;
    }

    private void OnAmmoChanged(int current, int max)
    {
        // First publish (or after weapon switch): seed _lastSeenAmmo without counting it as a shot.
        if (_lastSeenAmmo < 0)
        {
            _lastSeenAmmo = current;
            return;
        }

        // Ammo decreased = shot fired
        if (current < _lastSeenAmmo && !_firedShotThisSession)
        {
            _firedShotThisSession = true;
            Debug.Log("[StartMenuController] First shot detected — Start button unlocked.", this);
            UpdateStartLabel();
        }

        _lastSeenAmmo = current;
    }

    private void UpdateStartLabel()
    {
        if (_startLabelText == null) return;
        bool unlocked = IsStartUnlocked();
        _startLabelText.text  = unlocked ? _startReadyText  : _startLockedText;
        _startLabelText.color = unlocked ? _labelReadyColor : _labelLockedColor;
    }

    // ============================================================================
    // Tutorial pages
    // ============================================================================

    private void UpdatePageDisplay()
    {
        if (_pageText != null && _pageContents != null && _pageContents.Length > 0)
        {
            _currentPage = Mathf.Clamp(_currentPage, 0, _pageContents.Length - 1);
            _pageText.text = _pageContents[_currentPage];
        }
        if (_pageCounterText != null && _pageContents != null && _pageContents.Length > 0)
        {
            _pageCounterText.text = $"{_currentPage + 1} / {_pageContents.Length}";
        }
    }

    // ============================================================================
    // Button labels (mirrors PurchaseStation.BuildPriceLabel pattern)
    // ============================================================================

    private void BuildButtonLabels()
    {
        _labelRoots[0] = BuildButtonLabel(_startButtonAnchor, _startReadyText, "Label_Start", out _startLabelText);
        _labelRoots[1] = BuildButtonLabel(_quitButtonAnchor,  "QUIT",          "Label_Quit",  out _);
        _labelRoots[2] = BuildButtonLabel(_nextButtonAnchor,  "NEXT",          "Label_Next",  out _);
        _labelRoots[3] = BuildButtonLabel(_prevButtonAnchor,  "PREV",          "Label_Prev",  out _);
    }

    private GameObject BuildButtonLabel(Transform anchor, string initialText, string goName, out TextMeshProUGUI textRef)
    {
        textRef = null;
        if (anchor == null) return null;

        GameObject root = new GameObject(goName);
        root.transform.SetParent(anchor, worldPositionStays: false);
        root.transform.localPosition = _labelLocalOffset;
        root.transform.localRotation = Quaternion.identity;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        const float canvasW = 600f;
        const float canvasH = 200f;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(canvasW, canvasH);
        float scale = _labelWorldHeight / canvasH;
        canvasRect.localScale = new Vector3(scale, scale, scale);

        // BG (only if opacity > 0)
        if (_labelBgOpacity > 0f)
        {
            GameObject bgGO = new GameObject("BG", typeof(RectTransform));
            bgGO.transform.SetParent(root.transform, worldPositionStays: false);

            Image bg = bgGO.AddComponent<Image>();
            Color bgC = _labelBgColor;
            bgC.a = _labelBgOpacity;
            bg.color = bgC;
            bg.raycastTarget = false;

            RectTransform bgRect = bgGO.GetComponent<RectTransform>();
            float halfW = _labelBgWidthFraction * 0.5f;
            float halfH = _labelBgHeightFraction * 0.5f;
            bgRect.anchorMin = new Vector2(0.5f - halfW, 0.5f - halfH);
            bgRect.anchorMax = new Vector2(0.5f + halfW, 0.5f + halfH);
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        }

        // Text (added after BG so it renders on top)
        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(root.transform, worldPositionStays: false);

        TextMeshProUGUI t = textGO.AddComponent<TextMeshProUGUI>();
        t.text = initialText;
        t.fontSize = _labelFontSize;
        t.color = _labelReadyColor;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;

        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        textRef = t;
        return root;
    }

    private void BillboardLabel(GameObject label)
    {
        if (label == null) return;
        Transform camT = _cachedCamera.transform;
        label.transform.rotation = Quaternion.LookRotation(
            label.transform.position - camT.position,
            Vector3.up);
    }
}
