namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;
using Implementation.Shared.GodotExceptions;

/// <summary>Cap (upper-bound) boundary stage (int): clamps the running result down to <see cref="CapValue" />.</summary>
[GlobalClass, Tool]
public partial class IntCapStageRule : IntModifierStageRule
{
    /// <summary>The maximum the running result is allowed to reach.</summary>
    [Export] public int CapValue { get; private set; }

    public IntCapStageRule()
    {
        StageId = "Cap";
        Order = 500;
        NeutralValue = 0;
    }

    public IntCapStageRule(int capValue) : this()
    {
        CapValue = capValue;
    }

    public override int Reduce(int running, IReadOnlyList<int> stageValues)
        => Mathf.Min(running, CapValue);

    /// <summary>
    ///     A cap of 0 or below caps every stat to (at most) that value — almost always an unauthored
    ///     <c>.tres</c> left at the default 0. Fail fast rather than silently zeroing stats.
    /// </summary>
    public override void Validate()
    {
        if (CapValue <= 0)
        {
            throw new ResourceConfigurationException(
                $"IntCapStageRule.CapValue must be > 0 (was {CapValue}). A Cap rule left at the default 0 silently caps every stat to 0.",
                this);
        }
    }
}
