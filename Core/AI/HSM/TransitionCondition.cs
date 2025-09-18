namespace Jmodot.Core.AI.HSM;

using BB;

/// <summary>
/// A base class for condition resources used by StateTransitions.
/// These act as guards, preventing a state transition from occurring unless the condition is met.
/// Conditions should check state, not change it.
/// </summary>
[GlobalClass, Tool]
public abstract partial class TransitionCondition : Resource
{
    protected Node Agent { get; private set; }
    protected IBlackboard BB { get; private set; }

    /// <summary>
    /// Initializes the condition with the agent and blackboard context.
    /// </summary>
    public virtual void Init(Node agent, IBlackboard bb)
    {
        this.Agent = agent;
        this.BB = bb;
    }

    /// <summary>
    /// The core logic of the condition. Evaluates the condition and returns the result.
    /// This method should be fast and avoid side effects.
    /// </summary>
    /// <returns>True if the transition is allowed, otherwise false.</returns>
    public abstract bool Check();
}
