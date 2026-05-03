using UnityEngine;

public class bullet : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private string damageSourceId = "bullet";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckForPlayerHitbox(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckForPlayerHitbox(other);
    }

    private void CheckForPlayerHitbox(Collider hitCollider)
    {
        if (!PlayerDamageHelpers.TryDamagePlayer(hitCollider, damage, damageSourceId, hitCollider.ClosestPoint(transform.position), this))
        {
            return;
        }

        Debug.Log("Bullet hit the player.");
    }
}
