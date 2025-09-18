namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BehaviorTree.Conditions;
using Implementation.Shared;

/// <summary>
/// A condition that succeeds based on a random probability check.
/// This is a "guard" condition, meant to be checked once on entry.
/// Set MonitorConditions=false on the parent task for this behavior.
/// </summary>
[GlobalClass, Tool]
public partial class ProbabilityToRun : BTCondition
{
    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float RunProbability { get; private set; } = 0.5f;

    private bool _lastCheckResult = false;

    public override void OnParentTaskEnter()
    {
        // Roll the dice only once when the parent task enters.
        _lastCheckResult = MiscUtils.Rnd.NextSingle() < RunProbability;
    }

    public override bool Check()
    {
        // Return the result of the roll that was made on entry.
        return _lastCheckResult;
    }
}
