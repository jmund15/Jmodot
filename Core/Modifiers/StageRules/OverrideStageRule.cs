namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>
///     Override stage: replaces the running result with the highest-priority value, ignoring prior stages.
///     Relies on the strategy supplying stageValues sorted by Priority descending, so index 0 is the winner.
/// </summary>
[GlobalClass, Tool]
public partial class OverrideStageRule : FloatModifierStageRule
{
    public OverrideStageRule()
    {
        StageId = "Override";
        Order = 400;
        NeutralValue = 0f;
    }

    public override float Reduce(float running, IReadOnlyList<float> stageValues)
        => stageValues.Count > 0 ? stageValues[0] : running;
}
