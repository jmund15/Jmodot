namespace Jmodot.Core.AI.HSM;

using System.Collections.Generic;
using System.Linq;
using Godot.Collections;
using Implementation.AI.HSM;

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
}
