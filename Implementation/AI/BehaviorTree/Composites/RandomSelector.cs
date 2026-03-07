namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using Core.AI;
using Shared;

/// <summary>
/// A composite that evaluates children in a random order each time it's entered.
/// Succeeds as soon as one child succeeds, fails if all children fail.
/// Uses Fisher-Yates shuffle to produce a uniform random permutation on each OnEnter.
/// </summary>
[GlobalClass, Tool]
public partial class RandomSelector : CompositeTask
{
    private int[] _shuffledIndices = [];
    private int _currentIdx = -1;

    protected override void OnEnter()
    {
        base.OnEnter();

        if (ChildTasks.Count == 0)
        {
            Status = TaskStatus.Failure;
            return;
        }

        ShuffleIndices();
        _currentIdx = 0;
        var child = ChildTasks[_shuffledIndices[_currentIdx]];
        child.TaskStatusChanged += OnChildStatusChanged;
        child.Enter();
    }

    protected override void OnExit()
    {
        base.OnExit();
        if (_currentIdx != -1 && _currentIdx < _shuffledIndices.Length)
        {
            var child = ChildTasks[_shuffledIndices[_currentIdx]];
            child.TaskStatusChanged -= OnChildStatusChanged;
            child.Exit();
        }
        _currentIdx = -1;
    }

    protected override void OnProcessPhysics(float delta)
    {
        if (_currentIdx >= 0 && _currentIdx < _shuffledIndices.Length)
        {
            ChildTasks[_shuffledIndices[_currentIdx]].ProcessPhysics(delta);
        }
    }

    protected override void OnProcessFrame(float delta)
    {
        if (_currentIdx >= 0 && _currentIdx < _shuffledIndices.Length)
        {
            ChildTasks[_shuffledIndices[_currentIdx]].ProcessFrame(delta);
        }
    }

    private void OnChildStatusChanged(TaskStatus newStatus)
    {
        if (newStatus is TaskStatus.Running or TaskStatus.Fresh) { return; }

        var currentChild = ChildTasks[_shuffledIndices[_currentIdx]];
        currentChild.TaskStatusChanged -= OnChildStatusChanged;

        switch (newStatus)
        {
            case TaskStatus.Success:
                Status = TaskStatus.Success;
                break;

            case TaskStatus.Failure:
                _currentIdx++;
                if (_currentIdx >= _shuffledIndices.Length)
                {
                    Status = TaskStatus.Failure;
                }
                else
                {
                    var nextChild = ChildTasks[_shuffledIndices[_currentIdx]];
                    nextChild.TaskStatusChanged += OnChildStatusChanged;
                    nextChild.Enter();
                }
                break;
        }
    }

    private void ShuffleIndices()
    {
        _shuffledIndices = new int[ChildTasks.Count];
        for (int i = 0; i < ChildTasks.Count; i++)
        {
            _shuffledIndices[i] = i;
        }

        // Fisher-Yates shuffle
        for (int i = _shuffledIndices.Length - 1; i > 0; i--)
        {
            int j = JmoRng.Rnd.Next(i + 1);
            (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
        }
    }
}
