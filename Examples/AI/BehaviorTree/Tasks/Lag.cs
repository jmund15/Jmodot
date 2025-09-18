namespace Jmodot.Examples.AI.BehaviorTree.Tasks;

using Core.AI;
using Implementation.AI.BehaviorTree.Tasks;

/// <summary>
/// An action that waits for a specified duration and then succeeds.
/// </summary>
[GlobalClass, Tool]
public partial class Lag : BehaviorAction
{
    [Export(PropertyHint.Range, "0.0, 10.0, 0.1, or_greater")]
    protected float LagTime = 1.0f;

    private SceneTreeTimer _timer;

    protected override void OnEnter()
    {
        base.OnEnter();
        if (LagTime <= 0f)
        {
            Status = TaskStatus.SUCCESS;
            return;
        }

        _timer = GetTree().CreateTimer(LagTime);
        _timer.Timeout += OnLagTimeout;
    }

    protected override void OnExit()
    {
        base.OnExit();
        // Ensure the timer is cleaned up if the task is aborted.
        if (_timer.IsValid())
        {
            _timer.Timeout -= OnLagTimeout;
            // No need to QueueFree, SceneTreeTimer does this automatically.
        }
    }

    private void OnLagTimeout()
    {
        // Check if we are still running. It's possible the task was aborted
        // but the timer fired in the same frame.
        if (Status == TaskStatus.RUNNING)
        {
            Status = TaskStatus.SUCCESS;
        }
    }
}
