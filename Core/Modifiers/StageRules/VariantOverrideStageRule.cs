namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>Override stage (Variant): replaces the running result with the highest-priority value.</summary>
[GlobalClass, Tool]
public partial class VariantOverrideStageRule : VariantModifierStageRule
{
    public VariantOverrideStageRule()
    {
        StageId = "Override";
        Order = 400;
    }

    public override Variant Reduce(Variant running, IReadOnlyList<Variant> stageValues)
        => stageValues.Count > 0 ? stageValues[0] : running;
}
