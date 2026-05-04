using UnityEngine;

public class EndRoomCaller : MonoBehaviour
{
    public GameObject endDoor;
    public RefactoredDungeonGenerationManager dungeonManager;

    public void RemoveEndDoor()
    {
        if (dungeonManager == null)
        {
            dungeonManager = FindAnyObjectByType<RefactoredDungeonGenerationManager>();
        }

        if (dungeonManager != null)
        {
            dungeonManager.UnlockEndRoom();
            return;
        }

        if (endDoor != null)
        {
            endDoor.SetActive(false);
        }
    }
}
