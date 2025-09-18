namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using Core.AI;

/// <summary>
/// A composite task that executes its children in order until one succeeds. It succeeds
/// as soon as one of its children succeeds. It fails only if all of its children fail.
/// Also known as a "Fallback" node.
/// </summary>
[GlobalClass, Tool]
public partial class Selector : CompositeTask
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
            Status = TaskStatus.FAILURE;
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

    private void OnChildStatusChanged(TaskStatus newStatus)
    {
        if (newStatus is TaskStatus.RUNNING or TaskStatus.FRESH) { return; }

        var currentChild = ChildTasks[_runningChildIdx];
        currentChild.TaskStatusChanged -= OnChildStatusChanged;

        switch (newStatus)
        {
            case TaskStatus.SUCCESS:
                Status = TaskStatus.SUCCESS; // One child succeeded, so the selector succeeds
                break;

            case TaskStatus.FAILURE:
                _runningChildIdx++;
                if (_runningChildIdx >= ChildTasks.Count)
                {
                    Status = TaskStatus.FAILURE; // All children failed
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
