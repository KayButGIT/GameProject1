using UnityEngine;

// Stage progress shared between the title screen and the game scene.
public static class GameProgress
{
    private const string StageKey = "Bomberman.SavedStage";

    // The stage the title screen asked for. Zero means the game scene uses its own Starting Stage.
    public static int RequestedStage { get; set; }

    public static bool HasSave => PlayerPrefs.GetInt(StageKey, 0) >= 1;

    public static int SavedStage => Mathf.Max(1, PlayerPrefs.GetInt(StageKey, 1));

    public static void Save(int stage)
    {
        if (stage < 1) return;
        PlayerPrefs.SetInt(StageKey, stage);
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(StageKey);
        PlayerPrefs.Save();
    }
}
