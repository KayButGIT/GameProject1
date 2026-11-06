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
    public bool IsBonusStage { get; private set; }
    public int BonusRoundNumber { get; private set; }
    public System.Type BonusEnemyType { get; private set; }
    public StageSequence Sequence => sequence;
    /// <summary>Stages in the sequence, or 0 when there is none, which keeps the game endless.</summary>
    public int TotalStages => sequence != null ? sequence.TotalStages : 0;
    public bool OnLastStage => TotalStages > 0 && CurrentStage >= TotalStages && !IsBonusStage;
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
        IsBonusStage = false;
        BonusRoundNumber = 0;
        BonusEnemyType = null;
        // Continue on the title screen returns to the furthest stage reached.
        GameProgress.Save(stage);
        game.LoadStage(stage, theme);
    }

    public void AdvanceAfterClear()
    {
        if (!Application.isPlaying || game == null || CurrentStage < 1) return;

        if (IsBonusStage)
        {
            LoadStage(CurrentStage == int.MaxValue ? CurrentStage : CurrentStage + 1);
            return;
        }

        if (TryGetBonusAfter(CurrentStage, out int round, out System.Type enemyType))
        {
            IsBonusStage = true;
            BonusRoundNumber = round;
            BonusEnemyType = enemyType;
            GameProgress.Save(CurrentStage);
            game.LoadStage(CurrentStage, CurrentTheme);
            return;
        }

        NextStage();
    }

    public void NextStage()
    {
        if (CurrentStage < int.MaxValue) LoadStage(CurrentStage + 1);
    }

    public void RestartStage(bool randomizeLayout = false)
    {
        if (randomizeLayout) game?.RandomizeRestartLayout();
        if (IsBonusStage) game?.LoadStage(CurrentStage, CurrentTheme);
        else LoadStage(CurrentStage);
    }

    public static bool TryGetBonusAfter(int stage, out int round, out System.Type enemyType)
    {
        round = 0;
        enemyType = null;
        switch (stage)
        {
            case 5: round = 1; enemyType = typeof(ValcomEnemy); return true;
            case 10: round = 2; enemyType = typeof(OnealEnemy); return true;
            case 15: round = 3; enemyType = typeof(MinuoEnemy); return true;
            case 20: round = 4; enemyType = typeof(OvapeEnemy); return true;
            case 25: round = 5; enemyType = typeof(PassEnemy); return true;
            case 29: round = 6; enemyType = typeof(PontanEnemy); return true;
            default: return false;
        }
    }
}
