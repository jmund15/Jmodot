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
            if (child.Status == TaskStatus.RUNNING)
            {
                child.Exit();
            }
        }
    }

    private void OnChildStatusChanged(TaskStatus newStatus)
    {
        if (Status != TaskStatus.RUNNING)
        {
            return; // Already succeeded or failed
        }

        var successCount = ChildTasks.Count(c => c.Status == TaskStatus.SUCCESS);
        var failureCount = ChildTasks.Count(c => c.Status == TaskStatus.FAILURE);

        if (SuccessPolicy == Policy.RequireOne && successCount >= 1)
        {
            Status = TaskStatus.SUCCESS;
        }
        if (SuccessPolicy == Policy.RequireAll && successCount == ChildTasks.Count)
        {
            Status = TaskStatus.SUCCESS;
        }

        if (FailurePolicy == Policy.RequireOne && failureCount >= 1)
        {
            Status = TaskStatus.FAILURE;
        }
        if (FailurePolicy == Policy.RequireAll && failureCount == ChildTasks.Count)
        {
            Status = TaskStatus.FAILURE;
        }
    }
}
