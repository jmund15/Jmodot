namespace Jmodot.Core.AI.BehaviorTree.Conditions;

using BB;
using Implementation.AI.BehaviorTree.Tasks;
using Implementation.Shared;

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
    [ExportGroup("Abort Behavior")]
    [Export]
    public bool SucceedOnAbort { get; private set; }

    /// <summary>
    /// If true, this condition's abort bypasses CanAbort() — unconditional termination.
    /// Use for damage, death, or non-negotiable interrupts.
    /// Mirrors HSM's StateTransition.Urgent flag.
    /// </summary>
    [Export]
    public bool UrgentAbort { get; private set; }

    protected BehaviorTask OwnerTask { get; private set; } = null!;
    protected Node Agent { get; private set; } = null!;
    protected IBlackboard BB { get; private set; } = null!;

    /// <summary>
    /// Initializes the condition with its owner task, agent, and blackboard context.
    /// Called on cloned runtime instances during BehaviorTask.Init().
    /// </summary>
    public virtual void Init(BehaviorTask owner, Node agent, IBlackboard bb)
    {
        if (OwnerTask is not null && IsInstanceValid(OwnerTask) && OwnerTask != owner)
        {
            JmoLogger.Error(this, $"BTCondition '{ResourceName}' is being shared. " +
                "Each instance must belong to exactly one task.");
            return;
        }
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
    /// Must avoid external side effects (e.g., modifying blackboard, affecting other systems).
    /// Condition-internal state mutation (e.g., starting a cooldown timer) is acceptable
    /// when it's integral to the condition's own lifecycle.
    /// </summary>
    /// <returns>True if the condition is met, otherwise false.</returns>
    public abstract bool Check();
}
