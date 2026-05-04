using System.Collections;
using BNG;
using UnityEngine;

/// <summary>
/// Disables a target set of colliders for a brief window after each
/// <see cref="Damageable.onDamaged"/> event, so the same bullet / raycast can't
/// apply damage multiple times to the same target on subsequent frames.
///
/// <para><b>Wiring</b>: attach to the same GameObject as <see cref="Damageable"/>
/// on each enemy prefab. By default, picks up all child colliders at Awake; override
/// via the Inspector if you want only specific ones disabled (e.g. keep a NavMesh
/// blocker collider always on while disabling only the hitbox collider).</para>
///
/// <para><b>Limitation</b>: <see cref="Damageable.onDamaged"/> fires AFTER damage is
/// applied, so the very first hit lands. The i-frame prevents subsequent hits
/// within the window. Same-frame multi-hits (rare — multiple colliders hit on the
/// same physics tick) can still all register because the disable hasn't propagated
/// yet. Acceptable for alpha.</para>
///
/// <para><b>Asmdef boundary note</b>: this script lives in default Assembly-CSharp
/// (NOT in Jerry's asmdef) because it references BNG <see cref="Damageable"/>.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class IFrameOnDamage : MonoBehaviour
{
    [Header("I-Frame Window")]
    [Tooltip("Seconds to disable colliders after each damage event. " +
             "Tune lower (e.g. 0.05) if it blocks legitimate rapid follow-up shots.")]
    [Min(0f)]
    [SerializeField] private float _duration = 0.1f;

    [Header("Colliders to Disable")]
    [Tooltip("Colliders disabled during the i-frame window. If empty, " +
             "auto-populates from GetComponentsInChildren<Collider> at Awake.")]
    [SerializeField] private Collider[] _colliders;

    private Damageable _damageable;
    private Coroutine _activeRoutine;

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();
        if (_colliders == null || _colliders.Length == 0)
        {
            _colliders = GetComponentsInChildren<Collider>(includeInactive: false);
        }
    }

    private void OnEnable()
    {
        if (_damageable != null) _damageable.onDamaged.AddListener(OnDamaged);
    }

    private void OnDisable()
    {
        if (_damageable != null) _damageable.onDamaged.RemoveListener(OnDamaged);
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
            SetCollidersEnabled(true);
        }
    }

    private void OnDamaged(float _)
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(IFrameRoutine());
    }

    private IEnumerator IFrameRoutine()
    {
        SetCollidersEnabled(false);
        // WaitForSeconds respects Time.timeScale, but enemies are dead-frozen anyway
        // when the player dies (PSM sets timeScale=0), so this is fine — the i-frame
        // pauses with the rest of the world.
        yield return new WaitForSeconds(_duration);
        SetCollidersEnabled(true);
        _activeRoutine = null;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null) return;
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null) _colliders[i].enabled = enabled;
        }
    }
}
