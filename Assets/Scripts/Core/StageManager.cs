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
    public int StartingStage => startingStage;
    private BombermanPrototype game;

    internal void Initialize(BombermanPrototype owner)
    {
        game = owner;
        LoadStage(startingStage);
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
        game.LoadStage(stage, theme);
    }

    public void NextStage()
    {
        if (CurrentStage < int.MaxValue) LoadStage(CurrentStage + 1);
    }

    public void RestartStage() => LoadStage(CurrentStage);
}
