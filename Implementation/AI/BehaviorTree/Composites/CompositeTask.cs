namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Tasks;

/// <summary>
/// An abstract base class for tasks that have children, such as Sequence, Selector, and Parallel.
/// It manages the collection of child tasks and provides common functionality.
/// </summary>
[GlobalClass, Tool]
public abstract partial class CompositeTask : BehaviorTask
{
    public IReadOnlyList<BehaviorTask> ChildTasks => _childTasks;
    private List<BehaviorTask> _childTasks = new();

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        _childTasks = this.GetChildrenOfType<BehaviorTask>().ToList();
        foreach (var childTask in _childTasks)
        {
            childTask.Init(agent, bb);
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (!_childTasks.Any())
        {
            warnings.Add("Composite Task must have at least one child BehaviorTask.");
        }
        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }
}
