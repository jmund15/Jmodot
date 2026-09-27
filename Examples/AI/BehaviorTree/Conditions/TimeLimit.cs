namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.AI.BehaviorTree.Conditions;
using Implementation.Shared;

/// <summary>
/// A condition that monitors elapsed time. It fails (or succeeds, if SucceedOnAbort is true)
/// after a specified time limit has passed. This is a monitoring condition.
/// </summary>
[GlobalClass, Tool]
public partial class TimeLimit : BTCondition
{
    [Export(PropertyHint.Range, "0.0, 60.0, 0.1, or_greater")]
    public float Limit { get; private set; } = 1.0f;

    private double _startTime;
    private bool _isActive;

    public override void OnParentTaskEnter()
    {
        _startTime = GameClock.NowSeconds;
        _isActive = true;
    }

    public override void OnParentTaskExit()
    {
        _isActive = false;
    }

    public override bool Check()
    {
        if (!_isActive) { return true; }
        return GameClock.NowSeconds - _startTime < Limit;
    }
}
