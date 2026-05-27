namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>Floor (lower-bound) boundary stage (int): clamps the running result up to <see cref="FloorValue" />.</summary>
[GlobalClass, Tool]
public partial class IntFloorStageRule : IntModifierStageRule
{
    /// <summary>The minimum the running result is allowed to reach.</summary>
    [Export] public int FloorValue { get; private set; }

    public IntFloorStageRule()
    {
        StageId = "Floor";
        Order = 490;
        NeutralValue = 0;
    }

    public IntFloorStageRule(int floorValue) : this()
    {
        FloorValue = floorValue;
    }

    public override int Reduce(int running, IReadOnlyList<int> stageValues)
        => Mathf.Max(running, FloorValue);
}
