namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>Independent-multiply stage (int): sequentially multiplies the running result by each value.</summary>
[GlobalClass, Tool]
public partial class IntIndependentMultiplyStageRule : IntModifierStageRule
{
    public IntIndependentMultiplyStageRule()
    {
        StageId = "FinalMultiply";
        Order = 300;
        NeutralValue = 1;
    }

    public override int Reduce(int running, IReadOnlyList<int> stageValues)
    {
        var result = running;
        foreach (var value in stageValues)
        {
            result *= value;
        }
        return result;
    }
}
