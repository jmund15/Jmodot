namespace Jmodot.Core.AI.BehaviorTree.Conditions;

using BB;
using Implementation.AI.BehaviorTree.Tasks;

/// <summary>
/// A base class for condition resources used by BehaviorTasks.
/// Conditions are used to guard execution or monitor the state of a running task.
/// They should check state, not change it.
/// </summary>
[GlobalClass]
[Tool]
public abstract partial class BTCondition : Resource
{
    /// <summary>
    /// If a running task is aborted by this condition, should the task return SUCCESS?
    /// If false, the task will return FAILURE. This is useful for conditions like timers
    /// or counters where reaching the limit is a success condition.
    /// </summary>
    [Export]
    public bool SucceedOnAbort { get; private set; }

    protected BehaviorTask OwnerTask { get; private set; } = null!;
    protected Node Agent { get; private set; } = null!;
    protected IBlackboard BB { get; private set; } = null!;

    /// <summary>
    /// Initializes the condition with its owner task, agent, and blackboard context.
    /// This is called once when the Behavior Tree is initialized.
    /// </summary>
    public virtual void Init(BehaviorTask owner, Node agent, IBlackboard bb)
    {
        this.OwnerTask = owner;
        this.Agent = agent;
        this.BB = bb;
    }

    /// <summary>
    /// Called when the parent task is entered. Use for setting up listeners or initial state.
    /// </summary>
    public virtual void OnParentTaskEnter() { }

    /// <summary>
    /// Called when the parent task is exited. Use for cleanup.
    /// </summary>
    public virtual void OnParentTaskExit() { }

    /// <summary>
    /// The core logic of the condition. Evaluates the condition and returns the result.
    /// This method should be fast and avoid side effects.
    /// </summary>
    /// <returns>True if the condition is met, otherwise false.</returns>
    public abstract bool Check();
}
