namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>
///     Floor (lower-bound) boundary stage: clamps the running result up to <see cref="FloorValue" />.
///     Stage values are unused — the bound lives on the rule. Folds late (Order 490) so it bounds the result.
/// </summary>
[GlobalClass, Tool]
public partial class FloorStageRule : FloatModifierStageRule
{
    /// <summary>The minimum the running result is allowed to reach.</summary>
    [Export] public float FloorValue { get; private set; }

    public FloorStageRule()
    {
        StageId = "Floor";
        Order = 490;
        NeutralValue = 0f;
    }

    public FloorStageRule(float floorValue) : this()
    {
        FloorValue = floorValue;
    }

    public override float Reduce(float running, IReadOnlyList<float> stageValues)
        => Mathf.Max(running, FloorValue);
}
