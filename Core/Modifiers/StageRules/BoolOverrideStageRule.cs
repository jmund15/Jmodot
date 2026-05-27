namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>Override stage (bool): replaces the running result with the highest-priority value.</summary>
[GlobalClass, Tool]
public partial class BoolOverrideStageRule : BoolModifierStageRule
{
    public BoolOverrideStageRule()
    {
        StageId = "Override";
        Order = 400;
    }

    public override bool Reduce(bool running, IReadOnlyList<bool> stageValues)
        => stageValues.Count > 0 ? stageValues[0] : running;
}
