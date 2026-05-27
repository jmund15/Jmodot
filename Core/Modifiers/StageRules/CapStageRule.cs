namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>
///     Cap (upper-bound) boundary stage: clamps the running result down to <see cref="CapValue" />.
///     Stage values are unused — the bound lives on the rule. Folds last (Order 500) so it bounds the result.
/// </summary>
[GlobalClass, Tool]
public partial class CapStageRule : FloatModifierStageRule
{
    /// <summary>The maximum the running result is allowed to reach.</summary>
    [Export] public float CapValue { get; private set; }

    public CapStageRule()
    {
        StageId = "Cap";
        Order = 500;
        NeutralValue = 0f;
    }

    public CapStageRule(float capValue) : this()
    {
        CapValue = capValue;
    }

    public override float Reduce(float running, IReadOnlyList<float> stageValues)
        => Mathf.Min(running, CapValue);
}
