using UnityEngine;

/// <summary>
/// Inter-session save data wrapper around <see cref="PlayerPrefs"/>. Two values today:
/// <list type="bullet">
///   <item><see cref="TutorialCompleted"/> — true once the player has completed the
///         start-menu tutorial at least once. Skips the start-button lock on subsequent runs.</item>
///   <item><see cref="LevelsCompleted"/> — accumulating counter. <b>TODO Sam</b>: increment
///         this from your LevelManager when a level is cleared.</item>
/// </list>
///
/// <para>Call <see cref="ClearAll"/> via <c>SaveClearer</c> for testing.</para>
/// </summary>
public static class SaveData
{
    private const string KEY_TUTORIAL_COMPLETED = "tutorial_completed";
    private const string KEY_LEVELS_COMPLETED = "levels_completed";

    public static bool TutorialCompleted
    {
        get => PlayerPrefs.GetInt(KEY_TUTORIAL_COMPLETED, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(KEY_TUTORIAL_COMPLETED, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static int LevelsCompleted
    {
        get => PlayerPrefs.GetInt(KEY_LEVELS_COMPLETED, 0);
        set
        {
            PlayerPrefs.SetInt(KEY_LEVELS_COMPLETED, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }

    /// <summary>Increments <see cref="LevelsCompleted"/> by 1. Convenience for Sam's LevelManager.</summary>
    public static void IncrementLevelsCompleted()
    {
        LevelsCompleted = LevelsCompleted + 1;
    }

    /// <summary>Wipes all persisted save data. Used by SaveClearer for testing.</summary>
    public static void ClearAll()
    {
        PlayerPrefs.DeleteKey(KEY_TUTORIAL_COMPLETED);
        PlayerPrefs.DeleteKey(KEY_LEVELS_COMPLETED);
        PlayerPrefs.Save();
    }
}
