using UnityEngine;
using JerryScripts.Foundation;

public class bullet : MonoBehaviour
{
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
        if (hitCollider.GetComponent<PlayerHitbox>() == null)
        {
            return;
        }

        Debug.Log("Bullet hit the player.");
    }
}
