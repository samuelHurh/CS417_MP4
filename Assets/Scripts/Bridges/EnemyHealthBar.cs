using BNG;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating world-space HP bar above an enemy. Mirrors the player HP bar style
/// (10 segmented bars with gaps + boundary segment color-lerp) but in red. Updates
/// when sibling <see cref="Damageable.onDamaged"/> fires; hides on
/// <see cref="Damageable.onDestroyed"/>.
///
/// <para><b>Wiring</b>: attach to each enemy prefab (Chaser / Shooter / Support)
/// alongside the existing <see cref="Damageable"/> + <c>EnemyDeathRewards</c> +
/// <c>IFrameOnDamage</c> components. The bar auto-builds its canvas + segments in
/// Awake; tunable Inspector fields control vertical offset, panel size, segment
/// count, and colors.</para>
///
/// <para><b>Billboard</b>: rotates to face <see cref="Camera.main"/> each
/// <c>LateUpdate</c>. If no main camera exists (e.g. headset offline), the bar
/// stays at its authored rotation.</para>
///
/// <para><b>Asmdef boundary note</b>: lives in default Assembly-CSharp (no namespace)
/// because it references BNG <see cref="Damageable"/>.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class EnemyHealthBar : MonoBehaviour
{
    [Header("Position")]
    [Tooltip("Local offset from the enemy's origin to the bar's canvas root. " +
             "Default (0, 2.0, 0) puts it ~head-height above a humanoid enemy.")]
    [SerializeField] private Vector3 _localOffset = new Vector3(0f, 2.0f, 0f);

    [Header("Panel")]
    [Tooltip("Physical size of the bar canvas in metres (width x height).")]
    [SerializeField] private Vector2 _panelSize = new Vector2(0.5f, 0.05f);

    [Tooltip("Background opacity behind the segments.")]
    [Range(0f, 1f)]
    [SerializeField] private float _bgOpacity = 0.6f;

    [SerializeField] private Color _bgColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    [Header("Segments")]
    [Tooltip("Number of horizontal bar segments. Match the player HP for visual consistency (default 10).")]
    [Min(1)]
    [SerializeField] private int _segmentCount = 10;

    [Tooltip("Color of a fully-filled segment.")]
    [SerializeField] private Color _filledColor = new Color(0.9f, 0.15f, 0.15f, 1f);

    [Tooltip("Color of an empty segment.")]
    [SerializeField] private Color _emptyColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Tooltip("Fractional gap between segments (0.015 = 1.5% of bar width per gap).")]
    [Range(0f, 0.1f)]
    [SerializeField] private float _gapFraction = 0.015f;

    [Header("Lifecycle")]
    [Tooltip("Hide the bar when the enemy dies (Damageable.onDestroyed).")]
    [SerializeField] private bool _hideOnDeath = true;

    private Damageable _damageable;
    private float _maxHealth;
    private GameObject _canvasRoot;
    private Image[] _segments;
    private Camera _mainCamera;

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();
        // BNG.Damageable doesn't expose a MaxHealth getter; the public Health field's
        // initial value (set in the prefab) IS effectively the max. Cache it now.
        _maxHealth = Mathf.Max(0.001f, _damageable.Health);
        BuildCanvas();
        UpdateSegments(1f);  // start at full HP
    }

    private void OnEnable()
    {
        if (_damageable == null) return;
        _damageable.onDamaged.AddListener(OnDamaged);
        _damageable.onDestroyed.AddListener(OnDestroyedHandler);
    }

    private void OnDisable()
    {
        if (_damageable == null) return;
        _damageable.onDamaged.RemoveListener(OnDamaged);
        _damageable.onDestroyed.RemoveListener(OnDestroyedHandler);
    }

    private void OnDamaged(float damageAmount)
    {
        float ratio = _maxHealth > 0f
            ? Mathf.Clamp01(_damageable.Health / _maxHealth)
            : 0f;
        UpdateSegments(ratio);
    }

    private void OnDestroyedHandler()
    {
        if (_hideOnDeath && _canvasRoot != null) _canvasRoot.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_canvasRoot == null) return;
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }
        // Billboard: face the camera. Use the camera's forward direction so the bar
        // is always frontal regardless of pitch (avoids the bar "tipping over" if the
        // player crouches or looks up).
        Transform camT = _mainCamera.transform;
        _canvasRoot.transform.rotation = Quaternion.LookRotation(
            _canvasRoot.transform.position - camT.position,
            Vector3.up);
    }

    // ==============================================================================
    // Canvas construction
    // ==============================================================================

    private void BuildCanvas()
    {
        _canvasRoot = new GameObject("EnemyHealthBar_HUD");
        _canvasRoot.transform.SetParent(transform, worldPositionStays: false);
        _canvasRoot.transform.localPosition = _localOffset;
        _canvasRoot.transform.localRotation = Quaternion.identity;

        Canvas canvas = _canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        const float canvasW = 800f;
        const float canvasH = 80f;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(canvasW, canvasH);
        canvasRect.localScale = new Vector3(_panelSize.x / canvasW, _panelSize.y / canvasH, 1f);

        AddBackground(_canvasRoot);

        // Segment container fills the canvas with a small inset so segments don't touch the edges.
        GameObject segContainer = CreateChild(_canvasRoot, "Segments");
        RectTransform segRect = segContainer.GetComponent<RectTransform>();
        segRect.anchorMin = Vector2.zero;
        segRect.anchorMax = Vector2.one;
        segRect.offsetMin = new Vector2(8f, 8f);
        segRect.offsetMax = new Vector2(-8f, -8f);

        _segments = new Image[_segmentCount];
        float segWidth = (1f - _gapFraction * (_segmentCount - 1)) / _segmentCount;
        for (int i = 0; i < _segmentCount; i++)
        {
            float xMin = i * (segWidth + _gapFraction);

            GameObject segGO = CreateChild(segContainer, $"Seg_{i}");
            Image segImg = segGO.AddComponent<Image>();
            segImg.color = _emptyColor;
            segImg.raycastTarget = false;
            _segments[i] = segImg;

            RectTransform sr = segGO.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(xMin, 0f);
            sr.anchorMax = new Vector2(xMin + segWidth, 1f);
            sr.offsetMin = sr.offsetMax = Vector2.zero;
        }
    }

    private void AddBackground(GameObject parent)
    {
        GameObject bgGO = CreateChild(parent, "BG");
        Image bg = bgGO.AddComponent<Image>();
        Color c = _bgColor;
        c.a = _bgOpacity;
        bg.color = c;
        bg.raycastTarget = false;

        RectTransform r = bgGO.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static GameObject CreateChild(GameObject parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    // ==============================================================================
    // Segment fill (matches HUDSystem player-HP fractional pattern)
    // ==============================================================================

    private void UpdateSegments(float ratio)
    {
        if (_segments == null) return;

        float segmentsFilled = ratio * _segmentCount;
        int wholeFilled = Mathf.FloorToInt(segmentsFilled);
        float fractional = segmentsFilled - wholeFilled;

        for (int i = 0; i < _segments.Length; i++)
        {
            if (_segments[i] == null) continue;

            if (i < wholeFilled)
            {
                _segments[i].color = _filledColor;
            }
            else if (i == wholeFilled && fractional > 0f)
            {
                // Boundary segment — lerp colors by fractional fill, matching HUDSystem pattern
                _segments[i].color = Color.Lerp(_emptyColor, _filledColor, fractional);
            }
            else
            {
                _segments[i].color = _emptyColor;
            }
        }
    }
}
