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
    protected Node Agent { get; private set; } = null!;
    protected IBlackboard BB { get; private set; } = null!;

    /// <summary>
    /// Initializes the condition with the agent and blackboard context.
    /// </summary>
    public void Init(Node agent, IBlackboard bb)
    {
        Agent = agent;
        BB = bb;
        OnInit();
    }

    public virtual void OnInit() { }

    /// <summary>
    /// The core logic of the condition. Evaluates the condition and returns the result.
    /// This method should be fast and avoid side effects.
    /// </summary>
    /// <returns>True if the transition is allowed, otherwise false.</returns>
    public abstract bool Check();
}
