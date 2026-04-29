using UnityEngine;

namespace JerryScripts.Presentation.HUD
{
    /// <summary>
    /// ScriptableObject containing all designer-tunable parameters for the
    /// hand-mounted HUD display. Create one asset via the Assets menu and
    /// assign it in the Inspector on the <see cref="HUDSystem"/> component.
    ///
    /// <para>Keeping these values out of the MonoBehaviour allows tuning without
    /// code recompilation and supports multiple presets by swapping the asset.</para>
    /// </summary>
    /// <remarks>S2-003. GDD: ui-hud-system.md §Tuning Knobs.</remarks>
    [CreateAssetMenu(
        fileName = "HUDConfig",
        menuName  = "CS417/Presentation/HUD Config",
        order     = 1)]
    public sealed class HUDConfig : ScriptableObject
    {
        [Header("Hand Display — Mounting")]
        [Tooltip("Local position offset from the left controller transform.")]
        [SerializeField] private Vector3 _localOffset = new Vector3(0f, 0.08f, -0.05f);

        [Tooltip("Local rotation offset (Euler angles) — tilts panel toward the player's face.")]
        [SerializeField] private Vector3 _localRotation = new Vector3(-30f, 0f, 0f);

        [Header("Hand Display — Panel")]
        [Tooltip("Physical width of the hand display panel in metres.")]
        [Min(0.04f)]
        [SerializeField] private float _panelWidth = 0.12f;

        [Tooltip("Physical height of the hand display panel in metres.")]
        [Min(0.02f)]
        [SerializeField] private float _panelHeight = 0.04f;

        [Tooltip("Background opacity (0 = fully transparent, 1 = fully opaque).")]
        [Range(0f, 1f)]
        [SerializeField] private float _bgOpacity = 0.7f;

        [Tooltip("Background color for the hand display panel.")]
        [SerializeField] private Color _bgColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        [Tooltip("Text color for all HUD elements. Default: warm off-white (#E8E8D0).")]
        [SerializeField] private Color _textColor = new Color(0.91f, 0.91f, 0.82f, 1f); // #E8E8D0

        [Header("Health Bar")]
        [Tooltip("Fill color of the health bar at full HP.")]
        [SerializeField] private Color _healthBarFillColor = new Color(0.2f, 0.8f, 0.3f, 1f);

        [Tooltip("Background color of the health bar track.")]
        [SerializeField] private Color _healthBarBgColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [Header("Weapon Panel (HUD-06) — Block B")]
        [Tooltip("Physical height of the weapon panel (Block B) in metres. Default: 0.06m (ui-hud-system.md Tuning Knobs).")]
        [Min(0.02f)]
        [SerializeField] private float _weaponPanelHeight = 0.06f;

        [Tooltip(
            "Generic pistol silhouette texture shown on the weapon panel (left side, ~30×20mm). " +
            "Assign any Texture2D — a default-imported PNG works without changing import settings. " +
            "Null is acceptable at runtime — panel still functions, but no icon will render and " +
            "a warning is logged (ui-hud-system.md Rule 19).")]
        [SerializeField] private Texture2D _pistolSilhouette;

        [Tooltip(
            "Local position offset of the weapon panel (Block B) relative to the left controller. " +
            "Default: (0, 0.02, -0.05) places it just below Block A. Tweak Y to raise/lower, " +
            "Z to push toward/away from palm. Live-editable in Play mode for quick tuning.")]
        [SerializeField] private Vector3 _weaponPanelLocalOffset = new Vector3(0f, 0.02f, -0.05f);

        [Tooltip(
            "Local rotation (Euler) of the weapon panel (Block B) relative to the left controller. " +
            "Default: (-30, 0, 0) tilts the panel toward the player's face like Block A.")]
        [SerializeField] private Vector3 _weaponPanelLocalRotation = new Vector3(-30f, 0f, 0f);

        [Header("Font")]
        [Tooltip("Font asset for all HUD text. Drag any .ttf or .otf font asset here. " +
                 "If left empty, the system will attempt to use a built-in font.")]
        [SerializeField] private Font _font;

        // -------------------------------------------------------------------
        // Public accessors
        // -------------------------------------------------------------------

        /// <summary>Local position offset from the left controller transform.</summary>
        public Vector3 LocalOffset => _localOffset;

        /// <summary>Local rotation (Euler) — tilts panel toward player's face.</summary>
        public Vector3 LocalRotation => _localRotation;

        /// <summary>Physical panel width in metres. Default: 0.12m.</summary>
        public float PanelWidth => _panelWidth;

        /// <summary>Physical panel height in metres. Default: 0.04m.</summary>
        public float PanelHeight => _panelHeight;

        /// <summary>Background opacity. Default: 0.7.</summary>
        public float BgOpacity => _bgOpacity;

        /// <summary>Background color.</summary>
        public Color BgColor => _bgColor;

        /// <summary>Text color. Default: warm off-white (#E8E8D0).</summary>
        public Color TextColor => _textColor;

        /// <summary>Health bar fill color at full HP.</summary>
        public Color HealthBarFillColor => _healthBarFillColor;

        /// <summary>Health bar background track color.</summary>
        public Color HealthBarBgColor => _healthBarBgColor;

        /// <summary>
        /// Physical height of the weapon panel (Block B) in metres.
        /// Default: 0.06m. Total canvas height when weapon held: PanelHeight + WeaponPanelHeight.
        /// </summary>
        public float WeaponPanelHeight => _weaponPanelHeight;

        /// <summary>
        /// Local position offset of the HUD-06 weapon panel from the left controller.
        /// Tweak in Inspector to position Block B independently of Block A.
        /// </summary>
        public Vector3 WeaponPanelLocalOffset => _weaponPanelLocalOffset;

        /// <summary>
        /// Local rotation (Euler) of the HUD-06 weapon panel.
        /// </summary>
        public Vector3 WeaponPanelLocalRotation => _weaponPanelLocalRotation;

        /// <summary>
        /// Generic pistol silhouette texture for HUD-06 left column.
        /// May be null — HUDSystem logs a warning and renders the panel without the icon.
        /// Accepts any Texture2D — a default-imported PNG works without changing import settings.
        /// Jerry assigns the texture asset in editor work.
        /// </summary>
        public Texture2D PistolSilhouette => _pistolSilhouette;

        /// <summary>Font asset for HUD text. May be null (system falls back to built-in).</summary>
        public Font Font => _font;
    }
}
