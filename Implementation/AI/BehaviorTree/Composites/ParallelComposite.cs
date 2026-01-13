namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using System.Linq;
using Core.AI;

/// <summary>
/// A composite task that executes all of its children simultaneously. Its final status
/// depends on the specified success and failure policies.
/// </summary>
[GlobalClass, Tool]
public partial class ParallelComposite : CompositeTask
{
    public enum Policy
    {
        /// <summary>Requires one child to meet the condition.</summary>
        RequireOne,
        /// <summary>Requires all children to meet the condition.</summary>
        RequireAll
    }

    [Export] public Policy SuccessPolicy { get; private set; } = Policy.RequireOne;
    [Export] public Policy FailurePolicy { get; private set; } = Policy.RequireOne;

    protected override void OnEnter()
    {
        base.OnEnter();
        foreach (var child in ChildTasks)
        {
            child.TaskStatusChanged += OnChildStatusChanged;
            child.Enter();
        }
    }

    protected override void OnExit()
    {
        base.OnExit();
        foreach (var child in ChildTasks)
        {
            child.TaskStatusChanged -= OnChildStatusChanged;
            if (child.Status == TaskStatus.Running)
            {
                child.Exit();
            }
        }
    }

    private void OnChildStatusChanged(TaskStatus newStatus)
    {
        if (Status != TaskStatus.Running)
        {
            return; // Already succeeded or failed
        }

        var successCount = ChildTasks.Count(c => c.Status == TaskStatus.Success);
        var failureCount = ChildTasks.Count(c => c.Status == TaskStatus.Failure);

        if (SuccessPolicy == Policy.RequireOne && successCount >= 1)
        {
            Status = TaskStatus.Success;
        }
        if (SuccessPolicy == Policy.RequireAll && successCount == ChildTasks.Count)
        {
            Status = TaskStatus.Success;
        }

        if (FailurePolicy == Policy.RequireOne && failureCount >= 1)
        {
            Status = TaskStatus.Failure;
        }
        if (FailurePolicy == Policy.RequireAll && failureCount == ChildTasks.Count)
        {
            Status = TaskStatus.Failure;
        }
    }
}
