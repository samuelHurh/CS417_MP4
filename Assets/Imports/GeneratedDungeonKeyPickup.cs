using UnityEngine;

public class GeneratedDungeonKeyPickup : MonoBehaviour
{
    private RefactoredDungeonGenerationManager dungeonManager;
    private bool collected;

    public void Initialize(RefactoredDungeonGenerationManager manager)
    {
        dungeonManager = manager;
    }

    public void Collect()
    {
        if (collected)
        {
            return;
        }

        collected = true;

        if (dungeonManager != null)
        {
            dungeonManager.UnlockEndRoom();
        }

        gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        Collect();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collect();
    }
}
