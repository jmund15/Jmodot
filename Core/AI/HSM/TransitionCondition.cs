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
    /// <summary>
    /// The core logic of the condition. Evaluates the condition and returns the result.
    /// This method must be side-effect-free — it may be called multiple times per frame,
    /// and a passing check does NOT guarantee the transition will commit (CanExit may block).
    /// </summary>
    /// <param name="agent">The agent node.</param>
    /// <param name="bb">The blackboard.</param>
    /// <returns>True if the transition is allowed, otherwise false.</returns>
    public abstract bool Check(Node agent, IBlackboard bb);

    /// <summary>
    /// Called after a transition using this condition has been fully committed
    /// (target state entered, old state exited). Use for deferred side effects
    /// like consuming one-shot flags. Called on ALL conditions of the transition,
    /// regardless of operator short-circuiting during Check().
    /// Default: no-op.
    /// </summary>
    /// <param name="agent">The agent node.</param>
    /// <param name="bb">The blackboard.</param>
    public virtual void OnTransitionCommitted(Node agent, IBlackboard bb) { }
}
