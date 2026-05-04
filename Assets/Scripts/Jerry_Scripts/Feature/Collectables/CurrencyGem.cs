using JerryScripts.Core.PlayerState;
using JerryScripts.Foundation.Audio;
using JerryScripts.Foundation.Player;
using UnityEngine;

namespace JerryScripts.Feature.Collectables
{
    /// <summary>
    /// A pickup gem that bursts from a defeated enemy, magnet-flies to the player when
    /// nearby, and increments player currency on contact.
    ///
    /// <para><b>Lifecycle</b>:</para>
    /// <list type="number">
    ///   <item>Spawned by <see cref="SpawnBurst"/> with random radial+upward burst velocity.</item>
    ///   <item>Falls / settles under physics for a short delay (<see cref="_magnetDelay"/>).</item>
    ///   <item>If the player's <see cref="PlayerStateManager"/> is within
    ///         <see cref="_magnetRange"/> metres, switches to kinematic and lerps toward
    ///         the player at increasing speed.</item>
    ///   <item>On <see cref="OnTriggerEnter"/> with the player collider (auto-detected via
    ///         <c>GetComponentInParent&lt;PlayerStateManager&gt;</c>), awards
    ///         <see cref="_value"/> currency, posts <c>CurrencyPickup</c> audio, destroys self.</item>
    /// </list>
    ///
    /// <para><b>Prefab requirements</b>: the gem prefab must have a <c>Rigidbody</c>,
    /// a trigger <c>Collider</c>, and a visual mesh. The prefab's Rigidbody starts
    /// non-kinematic with gravity on so the burst settles naturally; this script
    /// switches it to kinematic when magnet range engages.</para>
    /// </summary>
    /// <remarks>Sprint Final, Phase 3.</remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CurrencyGem : MonoBehaviour
    {
        [Header("Value")]
        [Tooltip("Currency awarded to the player on pickup. Set by SpawnBurst at runtime.")]
        [Min(0)]
        [SerializeField] private int _value = 1;

        [Header("Burst")]
        [Tooltip("Upward burst velocity at spawn (m/s).")]
        [SerializeField] private float _burstUpSpeed = 3.5f;

        [Tooltip("Random radial burst velocity at spawn (m/s).")]
        [SerializeField] private Vector2 _burstRadialRange = new Vector2(1f, 2.5f);

        [Header("Magnet")]
        [Tooltip("Seconds after spawn before magnet behavior engages. Lets the burst settle visually.")]
        [Min(0f)]
        [SerializeField] private float _magnetDelay = 0.6f;

        [Tooltip("Maximum distance to player at which magnet engages (metres).")]
        [Min(0.1f)]
        [SerializeField] private float _magnetRange = 3f;

        [Tooltip("How fast the gem flies toward the player when in magnet range (m/s, scaled by closeness).")]
        [Min(0.1f)]
        [SerializeField] private float _magnetSpeed = 10f;

        [Header("Lifetime")]
        [Tooltip("Maximum lifetime in seconds — gem self-destroys if never collected.")]
        [Min(1f)]
        [SerializeField] private float _maxLifetime = 30f;

        private Rigidbody _rigidbody;
        private Transform _playerTransform;
        private float _spawnTime;
        private bool _magnetEngaged;

        // ===================================================================
        // Spawn helper
        // ===================================================================

        /// <summary>
        /// Instantiates <paramref name="count"/> copies of <paramref name="prefab"/> at
        /// <paramref name="position"/>, sets each one's currency value to
        /// <paramref name="valuePerGem"/>, and applies a random burst velocity.
        /// </summary>
        public static void SpawnBurst(GameObject prefab, Vector3 position, int count, int valuePerGem)
        {
            if (prefab == null || count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                GameObject gemGO = Instantiate(prefab, position, Random.rotation);
                CurrencyGem gem = gemGO.GetComponent<CurrencyGem>();
                if (gem != null)
                {
                    gem.SetValue(valuePerGem);
                }
            }
        }

        // ===================================================================
        // Unity lifecycle
        // ===================================================================

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            _spawnTime = Time.time;
            _magnetEngaged = false;
            ApplyBurstVelocity();
        }

        private void Update()
        {
            // Self-destruct safety net
            if (Time.time - _spawnTime > _maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (Time.time - _spawnTime < _magnetDelay) return;

            ResolvePlayerTransform();
            if (_playerTransform == null) return;

            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            if (distance > _magnetRange) return;

            // Engage magnet — switch to kinematic transform-based movement
            if (!_magnetEngaged)
            {
                _magnetEngaged = true;
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }

            // Speed scales up as the gem closes in (1 + (1 - distance/range))
            float speed = _magnetSpeed * (1f + Mathf.Clamp01(1f - distance / _magnetRange));
            transform.position = Vector3.MoveTowards(
                transform.position,
                _playerTransform.position,
                speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Identify the player by walking to PlayerHitboxReceiver from the collider's
            // hierarchy (same GO, parent, or child — covers both PlayerCollider on the
            // hitbox GO and any sub-collider Sam may add).
            var receiver = other.GetComponent<PlayerHitboxReceiver>()
                        ?? other.GetComponentInParent<PlayerHitboxReceiver>()
                        ?? other.GetComponentInChildren<PlayerHitboxReceiver>();
            if (receiver == null) return;

            // Look up PSM separately — it's on _Systems, not on the player rig.
            var psm = FindAnyObjectByType<PlayerStateManager>();
            if (psm == null) return;

            ((IPlayerStateWriter)psm).AddCurrency(_value);

            var audioService = FindAnyObjectByType<AudioFeedbackService>();
            audioService?.PostFeedbackEvent(new FeedbackEventData(
                FeedbackEvent.CurrencyPickup,
                transform.position,
                0f,
                FeedbackHand.None));

            Destroy(gameObject);
        }

        // ===================================================================
        // Internal
        // ===================================================================

        internal void SetValue(int value) => _value = Mathf.Max(0, value);

        private void ApplyBurstVelocity()
        {
            float radialMagnitude = Random.Range(_burstRadialRange.x, _burstRadialRange.y);
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radialMagnitude;
            Vector3 burst = radial + Vector3.up * _burstUpSpeed;

            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            _rigidbody.linearVelocity = burst;
        }

        private void ResolvePlayerTransform()
        {
            if (_playerTransform != null) return;

            // PlayerHitboxReceiver lives on a child of the BNG XR Rig — use ITS transform
            // as the magnet target. PlayerStateManager lives on _Systems (scene root, near
            // origin) and is NOT a useful position reference.
            var receiver = FindAnyObjectByType<PlayerHitboxReceiver>();
            if (receiver != null)
            {
                _playerTransform = receiver.transform;
            }
        }
    }
}
