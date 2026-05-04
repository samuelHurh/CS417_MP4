using UnityEngine;

/// <summary>
/// Testing utility — attach to any GameObject in any scene to expose a
/// "Clear All Save Data" right-click action in the Inspector.
///
/// <para>Optional <see cref="_clearOnAwake"/> toggle wipes saves at scene load
/// for fully-deterministic test runs (leave OFF in normal play scenes).</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class SaveClearer : MonoBehaviour
{
    [Header("Auto-clear")]
    [Tooltip("If true, ClearSaves() runs in Awake — useful for a dedicated test scene. " +
             "Leave OFF in production scenes or you'll wipe the player's save every load.")]
    [SerializeField] private bool _clearOnAwake = false;

    private void Awake()
    {
        if (_clearOnAwake) ClearSaves();
    }

    [ContextMenu("Clear All Save Data")]
    public void ClearSaves()
    {
        SaveData.ClearAll();
        Debug.Log("[SaveClearer] All save data cleared.", this);
    }

    [ContextMenu("Log Current Save Data")]
    public void LogSaveData()
    {
        Debug.Log(
            $"[SaveClearer] TutorialCompleted={SaveData.TutorialCompleted}, " +
            $"LevelsCompleted={SaveData.LevelsCompleted}",
            this);
    }
}
