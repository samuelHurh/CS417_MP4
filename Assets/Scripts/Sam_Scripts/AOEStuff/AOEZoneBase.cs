using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public abstract class AOEZoneBase : MonoBehaviour
{
    [Header("AOE Lifetime")]
    [SerializeField] private float activationDelay = 0.8f;
    [SerializeField] private float duration = 4f;
    [SerializeField] private float radius = 2f;

    [Header("Grounding")]
    [SerializeField] private bool groundOnStart = true;
    [SerializeField] private float groundingSampleRadius = 3f;

    [Header("Debug")]
    [SerializeField] private Transform radiusVisual;

    [Header("Reference To Visual")]
    [SerializeField] public GameObject myVisual;

    public float ActivationDelay => activationDelay;
    public float Duration => duration;
    public float Radius => radius;
    public bool IsActive { get; private set; }

    protected virtual void Start()
    {
        myVisual.SetActive(false);
        if (groundOnStart)
        {
            GroundToNavMesh();
        }

        ApplyRadiusToComponents();
        StartCoroutine(LifetimeCoroutine());
    }

    private IEnumerator LifetimeCoroutine()
    {
        yield return new WaitForSeconds(activationDelay);
        //activationDelay == time after landing on the ground
        IsActive = true;
        OnAOEActivated();

        yield return new WaitForSeconds(duration);

        IsActive = false;
        OnAOEExpired();
        Destroy(gameObject);
    }

    private void GroundToNavMesh()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, groundingSampleRadius, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }
    }

    private void ApplyRadiusToComponents()
    {
        SphereCollider sphereCollider = GetComponent<SphereCollider>();

        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = true;
            sphereCollider.radius = radius;
        }

        if (radiusVisual != null)
        {
            float diameter = radius * 2f;
            radiusVisual.localScale = new Vector3(diameter, radiusVisual.localScale.y, diameter);
        }
    }

    protected virtual void OnAOEActivated()
    {
    }

    protected virtual void OnAOEExpired()
    {
    }
}
