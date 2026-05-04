using System.Collections.Generic;
using UnityEngine;

public class PlayerEntryTrigger : MonoBehaviour
{
    public LayerMask playerLayermask;
    public RoomEventManager myManager;
    private bool hasTriggered;

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    

    void OnTriggerEnter(Collider other)
    {
        TryStartEncounter(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryStartEncounter(other);
    }

    private void TryStartEncounter(Collider other)
    {
        if (hasTriggered || !IsPlayerCollider(other))
        {
            return;
        }

        if (myManager == null)
        {
            Debug.LogWarning("PlayerEntryTrigger is missing a RoomEventManager reference.", this);
            return;
        }

        hasTriggered = true;
        Debug.Log("Collided with player");
        myManager.StartEncounter();
    }

    private bool IsPlayerCollider(Collider other)
    {
        if ((playerLayermask.value & (1 << other.gameObject.layer)) != 0)
        {
            return true;
        }

        if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)
        {
            return true;
        }

        Transform current = other.transform.parent;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
