using UnityEngine;

public class NextDungeonButtonCaller : MonoBehaviour
{
    public RefactoredDungeonGenerationManager dungeonManager;
    public float teleportDelay = 3f;

    private bool hasTriggered;

    public void AdvanceToNextDungeon()
    {
        if (hasTriggered)
        {
            return;
        }

        hasTriggered = true;

        if (dungeonManager == null)
        {
            dungeonManager = FindAnyObjectByType<RefactoredDungeonGenerationManager>();
        }

        if (dungeonManager == null)
        {
            Debug.LogWarning("No RefactoredDungeonGenerationManager found for next dungeon button.", this);
            hasTriggered = false;
            return;
        }

        dungeonManager.AdvanceToNextDungeonAfterDelay(teleportDelay);
    }
}
