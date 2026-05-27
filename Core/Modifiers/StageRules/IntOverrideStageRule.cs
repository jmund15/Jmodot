namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>Override stage (int): replaces the running result with the highest-priority value.</summary>
[GlobalClass, Tool]
public partial class IntOverrideStageRule : IntModifierStageRule
{
    public IntOverrideStageRule()
    {
        StageId = "Override";
        Order = 400;
        NeutralValue = 0;
    }

    public override int Reduce(int running, IReadOnlyList<int> stageValues)
        => stageValues.Count > 0 ? stageValues[0] : running;
}
