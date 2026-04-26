namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using Core.AI;

/// <summary>
/// Shared base for priority-fallback composites ("Selector family"). Executes children in declared
/// order; succeeds when any child succeeds, fails only when all children fail. Subclasses extend
/// this with per-tick behavior via <see cref="OnTickStart"/> — the empty default produces classic
/// memoizing <see cref="Selector"/> semantics; <see cref="ReactiveSelector"/> overrides the hook
/// to scan higher-priority siblings for preemption opportunities each tick.
/// <para>
/// Not intended for direct use as a composite — register a concrete subclass in the editor instead.
/// </para>
/// </summary>
[Tool]
public abstract partial class PrioritySelectorBase : CompositeTask
{
    /// <summary>
    /// Index of the currently running child within <see cref="CompositeTask.ChildTasks"/>, or -1
    /// when the composite is not active. Exposed to subclasses so per-tick hooks (e.g.,
    /// reactive preemption) can read and mutate the active slot.
    /// </summary>
    protected int _runningChildIdx = -1;

    protected override void OnEnter()
    {
        base.OnEnter();
        _runningChildIdx = 0;
        if (ChildTasks.Count > 0)
        {
            var child = ChildTasks[_runningChildIdx];
            child.TaskStatusChanged += OnChildStatusChanged;
            child.Enter();
        }
        else
        {
            // A selector with no children fails immediately.
            Status = TaskStatus.Failure;
        }
    }

    protected override void OnExit()
    {
        base.OnExit();
        if (_runningChildIdx != -1 && _runningChildIdx < ChildTasks.Count)
        {
            var child = ChildTasks[_runningChildIdx];
            child.TaskStatusChanged -= OnChildStatusChanged;
            child.Exit();
        }
        _runningChildIdx = -1;
    }

    protected override void OnProcessPhysics(float delta)
    {
        OnTickStart();
        if (_runningChildIdx >= 0 && _runningChildIdx < ChildTasks.Count)
        {
            ChildTasks[_runningChildIdx].ProcessPhysics(delta);
        }
    }

    protected override void OnProcessFrame(float delta)
    {
        OnTickStart();
        if (_runningChildIdx >= 0 && _runningChildIdx < ChildTasks.Count)
        {
            ChildTasks[_runningChildIdx].ProcessFrame(delta);
        }
    }

    /// <summary>
    /// Per-tick hook called BEFORE the running child's process method. Default: no-op (classic
    /// memoizing Selector). Override to scan siblings, swap the running slot, or otherwise mutate
    /// <see cref="_runningChildIdx"/> before the child ticks.
    /// <para>
    /// Subclasses that swap the running slot are responsible for unsubscribing the outgoing
    /// child's <see cref="BehaviorTask.TaskStatusChanged"/> and subscribing the incoming child's,
    /// in that order, to avoid re-entrant callbacks during the implicit Status mutations in
    /// <see cref="BehaviorTask.Exit"/> and <see cref="BehaviorTask.Enter"/>.
    /// </para>
    /// </summary>
    protected virtual void OnTickStart() { }

    /// <summary>
    /// Standard fallback cascade: on a child's terminal status, succeed (any child succeeded) or
    /// advance to the next child (current failed). Exposed as <c>protected</c> so subclasses can
    /// re-subscribe a newly-promoted child to the same handler during preemption.
    /// </summary>
    protected void OnChildStatusChanged(TaskStatus newStatus)
    {
        if (newStatus is TaskStatus.Running or TaskStatus.Fresh) { return; }

        var currentChild = ChildTasks[_runningChildIdx];
        currentChild.TaskStatusChanged -= OnChildStatusChanged;

        switch (newStatus)
        {
            case TaskStatus.Success:
                Status = TaskStatus.Success;
                break;

            case TaskStatus.Failure:
                _runningChildIdx++;
                if (_runningChildIdx >= ChildTasks.Count)
                {
                    Status = TaskStatus.Failure;
                }
                else
                {
                    var nextChild = ChildTasks[_runningChildIdx];
                    nextChild.TaskStatusChanged += OnChildStatusChanged;
                    nextChild.Enter();
                }
                break;
        }
    }
}
