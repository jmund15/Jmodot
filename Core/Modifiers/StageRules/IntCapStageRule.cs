namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

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
}
