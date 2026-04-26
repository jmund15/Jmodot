namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using Core.AI;

/// <summary>
/// A priority-fallback composite ("Priority Selector with conditional abort"). Identical to
/// <see cref="Selector"/> for terminal-status propagation (succeeds when any child succeeds,
/// fails only when all fail), but additionally polls higher-priority siblings each tick:
/// when a higher-priority child's <see cref="Tasks.BehaviorTask.ConditionsMet"/> becomes true
/// while a lower-priority child is running, the running child is exited and the higher-priority
/// child takes over.
/// <para>
/// Use this composite when long-running children have guard conditions that may become true
/// mid-execution and should be allowed to preempt. Canonical case: a ranged enemy whose attack
/// branch (gated by <c>RangeCheckCondition</c>) should preempt a flee/approach branch once the
/// enemy enters firing range, even if the approach action is still returning Running.
/// </para>
/// <para>
/// Children intended as preemption targets MUST have one or more conditions in their
/// <see cref="Tasks.BehaviorTask.Conditions"/> array. A child without conditions has
/// <see cref="Tasks.BehaviorTask.ConditionsMet"/> = true unconditionally; if such a child is
/// higher-priority than the running child, it will be selected on the very next tick — which
/// is fine if that's the intent (always-prefer-this-branch-when-reachable) but surprising if
/// the designer expected a guard.
/// </para>
/// <para>
/// Selector-vs-ReactiveSelector decision: choose <see cref="Selector"/> when long-running
/// children should NOT be preempted (the textbook "memoize first running child" semantic);
/// choose ReactiveSelector when a higher-priority guard transition should claim the wheel
/// from a lower-priority running action.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class ReactiveSelector : CompositeTask
{
    private int _runningChildIdx = -1;

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
        TryReactivePreempt();
        if (_runningChildIdx >= 0 && _runningChildIdx < ChildTasks.Count)
        {
            ChildTasks[_runningChildIdx].ProcessPhysics(delta);
        }
    }

    protected override void OnProcessFrame(float delta)
    {
        TryReactivePreempt();
        if (_runningChildIdx >= 0 && _runningChildIdx < ChildTasks.Count)
        {
            ChildTasks[_runningChildIdx].ProcessFrame(delta);
        }
    }

    /// <summary>
    /// Scans children with index &lt; _runningChildIdx (higher priority than the current).
    /// On the first such child whose conditions are met, preempts the current child and
    /// transfers execution to the higher-priority candidate. If the candidate's Enter fails
    /// (e.g., condition flipped between poll and enter, or other guard fires), the standard
    /// child-status cascade in <see cref="OnChildStatusChanged"/> advances to the next child
    /// — same fallback semantics as a normal Selector.
    /// </summary>
    private void TryReactivePreempt()
    {
        if (_runningChildIdx <= 0) { return; } // already at highest priority, or not running

        for (int i = 0; i < _runningChildIdx; i++)
        {
            var candidate = ChildTasks[i];
            if (!candidate.ConditionsMet()) { continue; }

            // Preempt: detach + exit current, attach + enter candidate.
            // Order matters: unsubscribe BEFORE Exit so the implicit Status = Fresh in
            // BehaviorTask.Exit doesn't re-enter OnChildStatusChanged.
            var current = ChildTasks[_runningChildIdx];
            current.TaskStatusChanged -= OnChildStatusChanged;
            current.Exit();

            _runningChildIdx = i;
            candidate.TaskStatusChanged += OnChildStatusChanged;
            candidate.Enter();
            return;
        }
    }

    private void OnChildStatusChanged(TaskStatus newStatus)
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
