using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class WeaponFireController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponCore weaponCore;
    [SerializeField] private XRGrabInteractable primaryGripInteractable;
    [SerializeField] private Rigidbody slideRigidbody;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform slideBlowbackSource;

    [Header("Shot")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 30f;
    [SerializeField] private float projectileLifetime = 5f;
    [SerializeField] private bool useHitscan;
    [SerializeField] private float hitscanDistance = 100f;
    [SerializeField] private LayerMask hitscanMask = Physics.DefaultRaycastLayers;
    [SerializeField] private float fireCooldown = 0.1f;
    [SerializeField] private bool automaticFire;
    [SerializeField] private float slideBlowbackForce = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip dryFireClip;

    private float nextFireTime;
    private bool triggerHeld;

    private void Reset()
    {
        weaponCore = GetComponent<WeaponCore>();
        primaryGripInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (primaryGripInteractable == null)
        {
            primaryGripInteractable = GetComponent<XRGrabInteractable>();
        }

        if (primaryGripInteractable == null)
        {
            return;
        }

        primaryGripInteractable.activated.AddListener(OnActivated);
        primaryGripInteractable.deactivated.AddListener(OnDeactivated);
    }

    private void OnDisable()
    {
        if (primaryGripInteractable == null)
        {
            return;
        }

        primaryGripInteractable.activated.RemoveListener(OnActivated);
        primaryGripInteractable.deactivated.RemoveListener(OnDeactivated);
    }

    private void Update()
    {
        if (automaticFire && triggerHeld)
        {
            TryFire();
        }
    }

    public bool TryFire()
    {
        if (Time.time < nextFireTime)
        {
            return false;
        }

        nextFireTime = Time.time + fireCooldown;

        if (weaponCore == null || !weaponCore.TryConsumeChamberedRound())
        {
            PlayClip(dryFireClip);
            return false;
        }

        SpawnShot();
        ApplySlideBlowback();
        PlayClip(fireClip);
        return true;
    }

    private void OnActivated(ActivateEventArgs args)
    {
        triggerHeld = true;
        TryFire();
    }

    private void OnDeactivated(DeactivateEventArgs args)
    {
        triggerHeld = false;
    }

    private void SpawnShot()
    {
        if (muzzle == null)
        {
            return;
        }

        if (useHitscan)
        {
            Debug.DrawRay(muzzle.position, muzzle.forward * hitscanDistance, Color.yellow, 0.5f);
            Physics.Raycast(muzzle.position, muzzle.forward, out _, hitscanDistance, hitscanMask);
            return;
        }

        if (projectilePrefab == null)
        {
            return;
        }

        GameObject projectileInstance = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);

        if (projectileInstance.TryGetComponent(out Rigidbody projectileRigidbody))
        {
            projectileRigidbody.AddForce(muzzle.forward * projectileSpeed, ForceMode.VelocityChange);
        }

        Destroy(projectileInstance, projectileLifetime);
    }

    private void ApplySlideBlowback()
    {
        if (slideRigidbody == null)
        {
            return;
        }

        Transform blowbackTransform = slideBlowbackSource != null
            ? slideBlowbackSource
            : muzzle != null
                ? muzzle
                : transform;

        slideRigidbody.AddForce(-blowbackTransform.forward * slideBlowbackForce, ForceMode.Impulse);
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip);
    }
}
