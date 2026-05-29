namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;
using Implementation.Shared.GodotExceptions;

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

    /// <summary>
    ///     A cap of 0 or below caps every stat to (at most) that value — almost always an unauthored
    ///     <c>.tres</c> left at the default 0 (Godot null-strips the value-type default on save).
    ///     Fail fast rather than silently zeroing stats. Suppression-to-zero is a multiply-by-zero
    ///     concern, not a cap.
    /// </summary>
    public override void Validate()
    {
        if (CapValue <= 0f)
        {
            throw new ResourceConfigurationException(
                $"CapStageRule.CapValue must be > 0 (was {CapValue}). A Cap rule left at the default 0 silently caps every stat to 0.",
                this);
        }
    }
}
