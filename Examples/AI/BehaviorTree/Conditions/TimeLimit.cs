namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.AI.BehaviorTree.Conditions;

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

    public override void OnParentTaskEnter()
    {
        _startTime = Time.GetTicksMsec();
    }

    public override bool Check()
    {
        // The condition is "true" as long as the time limit has NOT been exceeded.
        return (Time.GetTicksMsec() - _startTime) / 1000.0 < Limit;
    }
}
