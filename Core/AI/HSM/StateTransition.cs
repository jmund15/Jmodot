namespace Jmodot.Core.AI.HSM;

using Godot.Collections;
using Implementation.AI.HSM;

/// <summary>
/// A data resource that defines a potential transition from one state to another.
/// It bundles a target state with a set of conditions that must all be met
/// for the transition to be considered valid.
/// </summary>
[GlobalClass, Tool]
public partial class StateTransition : Resource
{
    /// <summary>
    /// The state to transition to if all conditions are met.
    /// </summary>
    [Export(PropertyHint.NodeType, "State")]
    public State TargetState { get; private set; }

    /// <summary>
    /// A list of conditions that must all return true for this transition to be valid.
    /// If this list is empty, the transition is considered always valid (if checked).
    /// </summary>
    [Export]
    public Array<TransitionCondition> Conditions { get; private set; } = new();
}
