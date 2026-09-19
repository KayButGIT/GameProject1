using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Bomberman/Stage Sequence")]
public sealed class StageSequence : ScriptableObject
{
    [Serializable]
    public sealed class Phase
    {
        public StageTheme Theme;
        [Min(1)] public int StageCount = 10;
    }

    public List<Phase> Phases = new();

    public bool Validate(out string error)
    {
        error = null;
        if (Phases == null || Phases.Count == 0) error = "Add at least one phase.";
        else
        {
            long total = 0;
            for (int i = 0; i < Phases.Count; i++)
            {
                Phase phase = Phases[i];
                if (phase == null || phase.Theme == null || phase.StageCount < 1)
                {
                    error = $"Phase {i + 1} needs a theme and a positive stage count.";
                    break;
                }
                total += phase.StageCount;
                if (total > int.MaxValue) { error = "Total stage count exceeds the supported range."; break; }
            }
        }
        return error == null;
    }

    public bool TryResolve(int stage, out int phaseIndex, out StageTheme theme)
    {
        phaseIndex = -1;
        theme = null;
        if (stage < 1 || !Validate(out _)) return false;
        long end = 0;
        for (int i = 0; i < Phases.Count; i++)
        {
            end += Phases[i].StageCount;
            if (stage <= end || i == Phases.Count - 1)
            {
                phaseIndex = i;
                theme = Phases[i].Theme;
                return true;
            }
        }
        return false;
    }
}
