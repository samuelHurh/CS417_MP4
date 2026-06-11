using System.Collections;
using System.Collections.Generic;
using BNG;
using UnityEngine;

[RequireComponent(typeof(RaycastWeapon))]
public sealed class ShotgunPelletBurst : MonoBehaviour
{
    [Header("Pellets")]
    [SerializeField] private int pelletCount = 6;
    [SerializeField] private float pelletDamage = 15f;
    [SerializeField] private float pelletShotForce = 32f;
    [SerializeField] private float spreadAngle = 18f;
    [SerializeField] private float pelletLifetime = 8f;
    [SerializeField] private float muzzleSpawnOffset = 0.18f;
    [SerializeField] private float pelletSpawnInterval = 0.006f;

    [Header("References")]
    [SerializeField] private RaycastWeapon weapon;
    [SerializeField] private GameObject projectilePrefab;

    private float lastProcessedShotTime = -1f;
    private Collider[] ownerColliders;

    public int PelletCount => pelletCount;
    public float PelletDamage => pelletDamage;
    public float PelletShotForce => pelletShotForce;
    public float SpreadAngle => spreadAngle;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (weapon != null)
        {
            // This component owns pellet projectile spawning. Leaving VRIF's single
            // projectile path enabled adds a center projectile and can make muzzle
            // particles look like they are spraying from odd side points.
            weapon.AlwaysFireProjectile = false;
            weapon.FireProjectileInSlowMo = false;
            lastProcessedShotTime = weapon.GetLastShotTime();
            if (weapon.onShootEvent == null)
            {
                weapon.onShootEvent = new UnityEngine.Events.UnityEvent();
            }

            weapon.onShootEvent.AddListener(FirePelletBurstFromEvent);
        }
    }

    private void OnDisable()
    {
        if (weapon != null)
        {
            weapon.onShootEvent.RemoveListener(FirePelletBurstFromEvent);
        }
    }

    private void LateUpdate()
    {
        if (weapon == null)
        {
            ResolveReferences();
        }

        if (weapon == null)
        {
            return;
        }

        float shotTime = weapon.GetLastShotTime();
        if (shotTime > 0f && !Mathf.Approximately(shotTime, lastProcessedShotTime))
        {
            FirePelletBurst();
        }
    }

    public void Configure(int newPelletCount, float newPelletDamage, float newPelletShotForce, float newSpreadAngle)
    {
        pelletCount = Mathf.Max(1, newPelletCount);
        pelletDamage = Mathf.Max(0f, newPelletDamage);
        pelletShotForce = Mathf.Max(0f, newPelletShotForce);
        spreadAngle = Mathf.Max(0f, newSpreadAngle);
    }

    private void ResolveReferences()
    {
        if (weapon == null)
        {
            weapon = GetComponent<RaycastWeapon>();
        }

        if (projectilePrefab == null && weapon != null)
        {
            projectilePrefab = weapon.ProjectilePrefab;
        }

        if (ownerColliders == null || ownerColliders.Length == 0)
        {
            Grabbable grabbable = GetComponentInParent<Grabbable>();
            ownerColliders = grabbable != null
                ? grabbable.GetComponentsInChildren<Collider>()
                : GetComponentsInChildren<Collider>();
        }
    }

    private void FirePelletBurstFromEvent()
    {
        FirePelletBurst();
    }

    private void FirePelletBurst()
    {
        if (weapon == null || projectilePrefab == null)
        {
            return;
        }

        Transform muzzle = weapon.MuzzlePointTransform != null ? weapon.MuzzlePointTransform : weapon.GetMuzzlePointTransform();
        if (muzzle == null)
        {
            return;
        }

        lastProcessedShotTime = Mathf.Max(weapon.GetLastShotTime(), Time.time);

        StartCoroutine(FirePelletBurstRoutine(muzzle.position, muzzle.forward, muzzle.up, muzzle.right));
    }

    private IEnumerator FirePelletBurstRoutine(Vector3 muzzlePosition, Vector3 muzzleForward, Vector3 muzzleUp, Vector3 muzzleRight)
    {
        List<Collider> spawnedPelletColliders = new List<Collider>();

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 pelletDirection = GetRandomSpreadDirection(muzzleForward, muzzleUp, muzzleRight);
            Quaternion pelletRotation = Quaternion.LookRotation(pelletDirection, muzzleUp);
            Vector3 pelletPosition = muzzlePosition + pelletDirection * muzzleSpawnOffset;
            GameObject pellet = Instantiate(projectilePrefab, pelletPosition, pelletRotation);
            Collider[] pelletColliders = pellet.GetComponentsInChildren<Collider>();
            IgnoreOwnerCollisions(pelletColliders);
            IgnorePelletCollisions(pelletColliders, spawnedPelletColliders);
            spawnedPelletColliders.AddRange(pelletColliders);

            Projectile projectile = pellet.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Damage = pelletDamage;
                projectile.HitFXPrefab = weapon.HitFXPrefab;
                projectile.ValidLayers = weapon.ValidLayers;
            }

            Rigidbody pelletBody = pellet.GetComponent<Rigidbody>();
            if (pelletBody == null)
            {
                pelletBody = pellet.GetComponentInChildren<Rigidbody>();
            }

            if (pelletBody != null)
            {
                pelletBody.linearVelocity = Vector3.zero;
                pelletBody.AddForce(pelletDirection * pelletShotForce, ForceMode.VelocityChange);
            }

            Destroy(pellet, pelletLifetime);

            if (pelletSpawnInterval > 0f && i < pelletCount - 1)
            {
                yield return new WaitForSeconds(pelletSpawnInterval);
            }
        }
    }

    private Vector3 GetRandomSpreadDirection(Vector3 forward, Vector3 up, Vector3 right)
    {
        if (spreadAngle <= 0f)
        {
            return forward;
        }

        Vector2 diskPoint = Random.insideUnitCircle * Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
        Vector3 spreadDirection = forward + right * diskPoint.x + up * diskPoint.y;
        return spreadDirection.normalized;
    }

    private void IgnoreOwnerCollisions(Collider[] pelletColliders)
    {
        if (ownerColliders == null || ownerColliders.Length == 0)
        {
            return;
        }

        for (int i = 0; i < pelletColliders.Length; i++)
        {
            Collider pelletCollider = pelletColliders[i];
            if (pelletCollider == null)
            {
                continue;
            }

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];
                if (ownerCollider != null)
                {
                    Physics.IgnoreCollision(pelletCollider, ownerCollider, true);
                }
            }
        }
    }

    private void IgnorePelletCollisions(Collider[] newPelletColliders, List<Collider> spawnedPelletColliders)
    {
        for (int i = 0; i < newPelletColliders.Length; i++)
        {
            Collider newPelletCollider = newPelletColliders[i];
            if (newPelletCollider == null)
            {
                continue;
            }

            for (int j = 0; j < spawnedPelletColliders.Count; j++)
            {
                Collider spawnedPelletCollider = spawnedPelletColliders[j];
                if (spawnedPelletCollider != null)
                {
                    Physics.IgnoreCollision(newPelletCollider, spawnedPelletCollider, true);
                }
            }
        }
    }
}
