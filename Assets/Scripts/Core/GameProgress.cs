using System;
using UnityEngine;

// Stage progress shared between the title screen and the game scene.
// Continue brings back the furthest stage plus the score, lives, and power-ups the run had when that stage began.
public static class GameProgress
{
    private const string StageKey = "Bomberman.SavedStage";
    private const string RunKey = "Bomberman.SavedRun";

    /// <summary>Score, lives, and power-ups at the start of a stage.</summary>
    [Serializable]
    public sealed class RunData
    {
        public int stage;
        public int score;
        public int lives;
        public PowerUpState powerUps = new();
    }

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

    /// <summary>Stores the run as it stands when <paramref name="stage"/> begins.</summary>
    public static void SaveRun(int stage, int score, int lives, PowerUpState powerUps)
    {
        if (stage < 1) return;
        RunData run = new()
        {
            stage = stage,
            score = Mathf.Max(0, score),
            lives = Mathf.Max(0, lives),
            powerUps = powerUps != null ? powerUps.Clone() : new PowerUpState()
        };
        PlayerPrefs.SetString(RunKey, JsonUtility.ToJson(run));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Loads the saved run, but only when it belongs to <paramref name="stage"/>. A save that holds just a
    /// stage number (older saves) or a different stage returns false, so the game starts that stage fresh.
    /// </summary>
    public static bool TryLoadRun(int stage, out RunData run)
    {
        run = null;
        string json = PlayerPrefs.GetString(RunKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return false;

        try
        {
            run = JsonUtility.FromJson<RunData>(json);
        }
        catch (ArgumentException)
        {
            run = null;
        }

        if (run == null || run.stage != stage)
        {
            run = null;
            return false;
        }

        run.powerUps ??= new PowerUpState();
        return true;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(StageKey);
        PlayerPrefs.DeleteKey(RunKey);
        PlayerPrefs.Save();
    }
}