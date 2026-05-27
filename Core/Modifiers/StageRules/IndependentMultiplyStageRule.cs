namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>Independent-multiply stage: sequentially multiplies the running result by each value.</summary>
[GlobalClass, Tool]
public partial class IndependentMultiplyStageRule : FloatModifierStageRule
{
    public IndependentMultiplyStageRule()
    {
        StageId = "FinalMultiply";
        Order = 300;
        NeutralValue = 1f;
    }

    public override float Reduce(float running, IReadOnlyList<float> stageValues)
    {
        var result = running;
        foreach (var value in stageValues)
        {
            result *= value;
        }
        return result;
    }
}
