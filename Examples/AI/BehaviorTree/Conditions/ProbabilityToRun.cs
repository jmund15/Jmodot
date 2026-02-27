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

    private bool _lastCheckResult;
    private bool _isActive;

    public override void OnParentTaskEnter()
    {
        _lastCheckResult = JmoRng.Rnd.NextSingle() < RunProbability;
        _isActive = true;
    }

    public override void OnParentTaskExit()
    {
        _isActive = false;
    }

    public override bool Check()
    {
        if (!_isActive) { return true; }
        return _lastCheckResult;
    }

    #region Test Helpers
#if TOOLS
    internal void SetRunProbability(float value) => RunProbability = value;
#endif
    #endregion
}
