using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BombermanPrototype))]
public sealed class StageManager : MonoBehaviour
{
    [SerializeField] private StageSequence sequence;
    [SerializeField, Min(1)] private int startingStage = 1;
    public int CurrentStage { get; private set; }
    public int CurrentPhase { get; private set; }
    public StageTheme CurrentTheme { get; private set; }
    public StageSequence Sequence => sequence;
    /// <summary>Stages in the sequence, or 0 when there is none, which keeps the game endless.</summary>
    public int TotalStages => sequence != null ? sequence.TotalStages : 0;
    public bool OnLastStage => TotalStages > 0 && CurrentStage >= TotalStages;
    public int StartingStage => startingStage;
    private BombermanPrototype game;

    internal void Initialize(BombermanPrototype owner)
    {
        game = owner;
        // The title screen picks the stage; opening this scene on its own uses Starting Stage.
        LoadStage(GameProgress.RequestedStage >= 1 ? GameProgress.RequestedStage : startingStage);
    }

    public void LoadStage(int stage)
    {
        if (!Application.isPlaying || game == null) return;
        if (stage < 1) { Debug.LogWarning("Stage number must be positive.", this); return; }
        int phase = -1;
        StageTheme theme = null;
        if (sequence != null && !sequence.TryResolve(stage, out phase, out theme))
        {
            sequence.Validate(out string error);
            Debug.LogWarning(error, sequence);
            return;
        }
        CurrentStage = stage;
        CurrentPhase = phase + 1;
        CurrentTheme = theme;
        // Continue on the title screen returns to the furthest stage reached.
        GameProgress.Save(stage);
        game.LoadStage(stage, theme);
    }

    public void NextStage()
    {
        if (CurrentStage < int.MaxValue) LoadStage(CurrentStage + 1);
    }

    public void RestartStage() => LoadStage(CurrentStage);
}
