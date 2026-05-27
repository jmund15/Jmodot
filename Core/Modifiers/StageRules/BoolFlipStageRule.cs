namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;
using System.Linq;

/// <summary>
///     Flip stage (bool): toggles the running result once per modifier in the stage. The modifier's
///     Value is unused — each entry is a flip. Folds before Override so an Override can still win.
/// </summary>
[GlobalClass, Tool]
public partial class BoolFlipStageRule : BoolModifierStageRule
{
    public BoolFlipStageRule()
    {
        StageId = "Flip";
        Order = 350;
    }

    public override bool Reduce(bool running, IReadOnlyList<bool> stageValues)
        => stageValues.Aggregate(running, (acc, _) => !acc);
}
