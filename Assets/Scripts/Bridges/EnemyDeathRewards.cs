using BNG;
using JerryScripts.Feature.Collectables;
using JerryScripts.Foundation.Audio;
using UnityEngine;

/// <summary>
/// Bridges Sam's BNG-based enemies into Jerry's audio + currency systems.
/// Subscribes to the sibling <see cref="Damageable"/> for two events:
///
/// <list type="bullet">
///   <item><c>onDamaged(float)</c> — posts <c>FeedbackEvent.HitConfirmation</c> so the
///         player gets audible feedback that their shot connected.</item>
///   <item><c>onDestroyed</c> — calls <see cref="CurrencyGem.SpawnBurst"/> to drop loot.</item>
/// </list>
///
/// <para><b>Wiring</b>: attach to the same GameObject as <see cref="Damageable"/>
/// on each enemy prefab (Chaser / Shooter / Support).</para>
///
/// <para><b>Asmdef boundary note</b>: this script lives in default Assembly-CSharp (NOT in
/// Jerry's asmdef) because it references BNG <see cref="Damageable"/>. It can still
/// reference Jerry's <c>CurrencyGem</c> + <c>AudioFeedbackService</c> because default
/// assembly implicitly references all asmdefs.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class EnemyDeathRewards : MonoBehaviour
{
    [Header("Currency Drop")]
    [Tooltip("Prefab to spawn on enemy death. Must have a CurrencyGem component on the root.")]
    [SerializeField] private GameObject _gemPrefab;

    [Tooltip("Number of gems to burst on death.")]
    [Min(0)]
    [SerializeField] private int _gemCount = 3;

    [Tooltip("Currency value awarded per gem on pickup.")]
    [Min(0)]
    [SerializeField] private int _valuePerGem = 1;

    [Header("Hit Feedback")]
    [Tooltip("Post HitConfirmation audio when this enemy is damaged. " +
             "Set false if Sam's BNG damage path already plays a hit sound and " +
             "you don't want a double-up.")]
    [SerializeField] private bool _postHitConfirmation = true;

    private Damageable _damageable;

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        if (_damageable == null) return;

        if (_postHitConfirmation)
        {
            _damageable.onDamaged.AddListener(OnDamaged);
        }

        _damageable.onDestroyed.AddListener(OnDestroyedHandler);
    }

    private void OnDisable()
    {
        if (_damageable == null) return;

        if (_postHitConfirmation)
        {
            _damageable.onDamaged.RemoveListener(OnDamaged);
        }

        _damageable.onDestroyed.RemoveListener(OnDestroyedHandler);
    }

    private void OnDamaged(float damageAmount)
    {
        var audioService = FindAnyObjectByType<AudioFeedbackService>();
        audioService?.PostFeedbackEvent(new FeedbackEventData(
            FeedbackEvent.HitConfirmation,
            transform.position,
            damageAmount,
            FeedbackHand.None));
    }

    private void OnDestroyedHandler()
    {
        // Post enemy-death audio. Lives outside the gem-spawn guard so the sound
        // plays even if no gem prefab is assigned.
        var audioService = FindAnyObjectByType<AudioFeedbackService>();
        audioService?.PostFeedbackEvent(new FeedbackEventData(
            FeedbackEvent.EnemyDeath,
            transform.position,
            0f,
            FeedbackHand.None));

        if (_gemPrefab == null || _gemCount <= 0) return;

        CurrencyGem.SpawnBurst(_gemPrefab, transform.position, _gemCount, _valuePerGem);
    }
}
