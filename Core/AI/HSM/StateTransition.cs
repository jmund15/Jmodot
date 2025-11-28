namespace Jmodot.Core.AI.HSM;

using Godot.Collections;

// TODO: use "lego builder" pattern - create a fluent builder that chains logic of checks together for a TransitionCondition
// this will allow not just "all conditions must be met" logic, but a intelligent system of chained logic for state transitions
// uncertain if this is possible in the godot inspector, will need more thought

/// <summary>
/// A data resource that defines a potential transition from one state to another.
/// It bundles a path to a target state with a set of conditions that must all be met
/// for the transition to be considered valid. This resource is scene-independent.
/// </summary>
[GlobalClass, Tool]
public partial class StateTransition : Resource
{
    /// <summary>
    /// The path to the state to transition to if all conditions are met.
    /// This path is relative to the State node that owns this transition.
    /// </summary>
    [Export(PropertyHint.NodePathValidTypes, "State")]
    public NodePath TargetStatePath { get; private set; }

    /// <summary>
    /// A list of conditions that must all return true for this transition to be valid.
    /// If this list is empty, the transition is considered always valid (if checked).
    /// </summary>
    [Export]
    public Array<TransitionCondition> Conditions { get; private set; } = new();

    [Export] public bool CanPropagateUp { get; private set; } = false;

    /// <summary>
    /// If true, this transition bypasses the old state's CanExit() and ExitHandshake().
    /// Use for urgent transitions like being hit, damaged, or interrupted.
    /// </summary>
    [Export] public bool Urgent { get; private set; } = false;
}
