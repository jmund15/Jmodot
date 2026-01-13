namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using Core.AI;

/// <summary>
/// A composite task that executes its children in order. It succeeds only if all
/// of its children succeed. It fails as soon as one of its children fails.
/// </summary>
[GlobalClass, Tool]
public partial class Sequence : CompositeTask
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
            // A sequence with no children succeeds immediately.
            Status = TaskStatus.Success;
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
        if (newStatus is TaskStatus.Running or TaskStatus.Fresh) { return; }

        var currentChild = ChildTasks[_runningChildIdx];
        currentChild.TaskStatusChanged -= OnChildStatusChanged;

        switch (newStatus)
        {
            case TaskStatus.Success:
                _runningChildIdx++;
                if (_runningChildIdx >= ChildTasks.Count)
                {
                    Status = TaskStatus.Success; // All children succeeded
                }
                else
                {
                    var nextChild = ChildTasks[_runningChildIdx];
                    nextChild.TaskStatusChanged += OnChildStatusChanged;
                    nextChild.Enter();
                }
                break;

            case TaskStatus.Failure:
                Status = TaskStatus.Failure; // One child failed, so the sequence fails
                break;
        }
    }
}
