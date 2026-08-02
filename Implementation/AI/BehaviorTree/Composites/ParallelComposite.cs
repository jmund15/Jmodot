namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using Core.AI;
using Tasks;

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

    /// <summary>
    /// How each child ENDED, recorded at the moment it terminated. The live <c>child.Status</c> cannot
    /// answer that here: the composite contract Exits a self-terminated child immediately, and Exit
    /// writes <c>Fresh</c> — so policy math reading live status would forget every verdict it needs.
    /// <see cref="TaskStatus.Running"/> is the not-yet-terminal sentinel.
    /// </summary>
    private TaskStatus[] _terminalStatuses = [];

    /// <summary>
    /// Per-child handlers, kept so each subscription can be removed individually. A shared handler
    /// cannot tell the composite WHICH child fired, and the snapshot needs to know.
    /// </summary>
    private BehaviorTask.TaskStatusChangedEventHandler?[] _childHandlers = [];

    protected override void OnEnter()
    {
        base.OnEnter();

        var count = ChildTasks.Count;
        _terminalStatuses = new TaskStatus[count];
        _childHandlers = new BehaviorTask.TaskStatusChangedEventHandler?[count];

        for (var i = 0; i < count; i++)
        {
            var index = i;
            var child = ChildTasks[i];
            _terminalStatuses[i] = TaskStatus.Running;
            _childHandlers[i] = status => OnChildStatusChanged(index, status);
            child.TaskStatusChanged += _childHandlers[i];
            child.Enter();
        }
    }

    protected override void OnExit()
    {
        base.OnExit();

        for (var i = 0; i < ChildTasks.Count; i++)
        {
            var child = ChildTasks[i];
            if (i < _childHandlers.Length && _childHandlers[i] != null)
            {
                child.TaskStatusChanged -= _childHandlers[i];
                _childHandlers[i] = null;
            }

            // Unconditional, not gated on Running: a child torn down externally is left un-exited by a
            // status guard, and BehaviorTask.Exit is already idempotent so the extra call costs nothing.
            child.Exit();
        }
    }

    protected override void OnProcessPhysics(float delta)
    {
        foreach (var child in ChildTasks)
        {
            if (child.Status == TaskStatus.Running) { child.ProcessPhysics(delta); }
        }
    }

    protected override void OnProcessFrame(float delta)
    {
        foreach (var child in ChildTasks)
        {
            if (child.Status == TaskStatus.Running) { child.ProcessFrame(delta); }
        }
    }

    private void OnChildStatusChanged(int index, TaskStatus newStatus)
    {
        if (newStatus is TaskStatus.Running or TaskStatus.Fresh) { return; }
        if (index >= _terminalStatuses.Length) { return; }

        _terminalStatuses[index] = newStatus;

        var child = ChildTasks[index];
        // Unsubscribe BEFORE Exit — Exit writes Status = Fresh, which would re-enter this handler.
        child.TaskStatusChanged -= _childHandlers[index];
        _childHandlers[index] = null;
        child.Exit();

        if (Status != TaskStatus.Running) { return; }

        var successCount = 0;
        var failureCount = 0;
        foreach (var recorded in _terminalStatuses)
        {
            if (recorded == TaskStatus.Success) { successCount++; }
            else if (recorded == TaskStatus.Failure) { failureCount++; }
        }

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
