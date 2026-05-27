namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>Flat additive stage (int): sequentially adds each value onto the running result.</summary>
[GlobalClass, Tool]
public partial class IntAdditiveStageRule : IntModifierStageRule
{
    public IntAdditiveStageRule()
    {
        StageId = "BaseAdd";
        Order = 100;
        NeutralValue = 0;
    }

    public override int Reduce(int running, IReadOnlyList<int> stageValues)
    {
        var result = running;
        foreach (var value in stageValues)
        {
            result += value;
        }
        return result;
    }
}
