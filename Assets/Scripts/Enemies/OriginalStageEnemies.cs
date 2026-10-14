using System;
using System.Collections.Generic;

public static class OriginalStageEnemies
{
    public const int StageCount = 50;
    public const int MaxEnemies = 10;

    // ROM enemy ids; 0 is an empty slot.
    private static readonly Type[] TypesById =
    {
        null,
        typeof(ValcomEnemy),
        typeof(OnealEnemy),
        typeof(DahlEnemy),
        typeof(MinuoEnemy),
        typeof(OvapeEnemy),
        typeof(DoriaEnemy),
        typeof(PassEnemy),
        typeof(PontanEnemy)
    };

    // Ten enemy slots per stage.
    private static readonly byte[,] Roster =
    {
        { 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 }, // 1
        { 1, 1, 1, 2, 2, 2, 0, 0, 0, 0 },
        { 1, 1, 2, 2, 3, 3, 0, 0, 0, 0 },
        { 1, 2, 3, 3, 4, 4, 0, 0, 0, 0 },
        { 2, 2, 2, 2, 3, 3, 3, 0, 0, 0 }, // 5
        { 2, 2, 3, 3, 3, 4, 4, 0, 0, 0 },
        { 2, 2, 3, 3, 3, 5, 5, 0, 0, 0 },
        { 2, 3, 3, 4, 4, 4, 4, 0, 0, 0 },
        { 2, 3, 4, 4, 4, 4, 6, 0, 0, 0 },
        { 2, 3, 4, 5, 6, 6, 6, 0, 0, 0 }, // 10
        { 2, 3, 3, 4, 4, 4, 5, 6, 0, 0 },
        { 2, 3, 4, 5, 6, 6, 6, 6, 0, 0 },
        { 3, 3, 3, 4, 4, 4, 6, 6, 0, 0 },
        { 5, 5, 5, 5, 5, 5, 5, 7, 0, 0 },
        { 3, 4, 4, 4, 6, 6, 6, 7, 0, 0 }, // 15
        { 4, 4, 4, 6, 6, 6, 6, 7, 0, 0 },
        { 3, 3, 3, 3, 3, 6, 6, 7, 0, 0 },
        { 1, 1, 1, 2, 2, 2, 7, 7, 0, 0 },
        { 1, 2, 3, 3, 3, 5, 7, 7, 0, 0 },
        { 2, 3, 4, 5, 6, 6, 7, 7, 0, 0 }, // 20
        { 5, 5, 5, 6, 6, 6, 6, 7, 7, 0 },
        { 3, 3, 3, 3, 4, 4, 4, 6, 7, 0 },
        { 3, 3, 4, 4, 5, 5, 6, 6, 7, 0 },
        { 3, 4, 5, 6, 6, 5, 6, 6, 7, 0 },
        { 2, 2, 3, 4, 5, 5, 6, 6, 7, 0 }, // 25
        { 1, 2, 3, 4, 5, 5, 6, 6, 7, 0 },
        { 1, 2, 6, 6, 6, 5, 6, 6, 7, 0 },
        { 2, 3, 3, 3, 4, 4, 4, 6, 7, 0 },
        { 5, 5, 5, 5, 5, 6, 7, 6, 7, 0 },
        { 3, 3, 3, 4, 4, 5, 5, 6, 7, 0 }, // 30
        { 2, 2, 3, 3, 4, 4, 5, 5, 6, 6 },
        { 2, 3, 4, 4, 4, 6, 6, 6, 6, 7 },
        { 3, 3, 4, 4, 5, 6, 6, 7, 6, 7 },
        { 3, 3, 4, 4, 4, 6, 6, 7, 6, 7 },
        { 3, 3, 4, 5, 0, 6, 6, 7, 6, 7 }, // 35
        { 3, 3, 4, 4, 6, 6, 7, 6, 7, 7 },
        { 3, 3, 4, 5, 6, 6, 7, 6, 7, 7 },
        { 3, 3, 4, 4, 6, 6, 7, 6, 7, 7 },
        { 3, 4, 5, 5, 6, 7, 6, 7, 7, 7 },
        { 3, 4, 4, 6, 6, 7, 6, 7, 7, 7 }, // 40
        { 3, 4, 5, 6, 6, 6, 7, 7, 7, 7 },
        { 4, 5, 6, 6, 7, 6, 7, 7, 7, 7 },
        { 4, 5, 6, 7, 6, 7, 7, 7, 7, 7 },
        { 4, 5, 6, 7, 6, 7, 7, 7, 7, 7 },
        { 5, 6, 5, 6, 7, 7, 7, 7, 7, 7 }, // 45
        { 5, 6, 5, 6, 7, 7, 7, 7, 7, 7 },
        { 6, 5, 5, 6, 7, 7, 7, 7, 7, 7 },
        { 6, 5, 6, 7, 7, 7, 7, 7, 7, 8 },
        { 5, 5, 6, 7, 7, 7, 7, 7, 7, 8 },
        { 5, 5, 6, 7, 7, 7, 7, 7, 8, 8 } // 50
    };

    // The enemy released when a blast hits the revealed exit, one per stage.
    private static readonly byte[] ExitEnemies =
    {
        2, 1, 5, 3, 1, 1, 2, 5, 6, 4,
        1, 1, 5, 6, 2, 4, 1, 6, 1, 5,
        6, 5, 1, 5, 6, 8, 2, 1, 5, 7,
        4, 1, 5, 8, 6, 7, 5, 2, 4, 8,
        5, 4, 6, 5, 8, 4, 6, 5, 7, 8
    };

    public static IEnumerable<(Type type, int count)> GetRoster(int stage, int campaignLength = 0)
    {
        int row = Row(ResolveReferenceStage(stage, campaignLength));
        for (int id = 1; id < TypesById.Length; id++)
        {
            int count = 0;
            for (int slot = 0; slot < MaxEnemies; slot++)
            {
                if (Roster[row, slot] == id)
                {
                    count++;
                }
            }

            if (count > 0)
            {
                yield return (TypesById[id], count);
            }
        }
    }

    public static Type GetExitEnemy(int stage, int campaignLength = 0)
    {
        return TypesById[ExitEnemies[Row(ResolveReferenceStage(stage, campaignLength))]];
    }
    
    public static int ResolveReferenceStage(int stage, int campaignLength)
    {
        stage = Math.Max(1, stage);

        if (campaignLength <= 0)
        {
            if (stage <= StageCount) return stage;
            return StageCount - 9 + ((stage - StageCount - 1) % 10);
        }

        if (campaignLength == 1) return 1;

        stage = Math.Clamp(stage, 1, campaignLength);
        double progress = (stage - 1d) / (campaignLength - 1d);
        double scaledStage = 1d + (StageCount - 1d) * Math.Pow(progress, 1.4d);
        return Math.Clamp((int)Math.Floor(scaledStage + 0.5d), 1, StageCount);
    }

    private static int Row(int referenceStage)
    {
        return Math.Clamp(referenceStage, 1, StageCount) - 1;
    }
}
