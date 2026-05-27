namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>Flat additive stage: sequentially adds each value onto the running result.</summary>
[GlobalClass, Tool]
public partial class AdditiveStageRule : FloatModifierStageRule
{
    public AdditiveStageRule()
    {
        StageId = "BaseAdd";
        Order = 100;
        NeutralValue = 0f;
    }

    public override float Reduce(float running, IReadOnlyList<float> stageValues)
    {
        var result = running;
        foreach (var value in stageValues)
        {
            result += value;
        }
        return result;
    }
}
