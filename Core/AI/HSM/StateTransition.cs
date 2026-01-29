namespace Jmodot.Core.AI.HSM;

using System.Linq;
using BB;
using Godot.Collections;

/// <summary>
/// Determines how multiple conditions are combined when evaluating a transition.
/// </summary>
public enum ConditionOperator
{
    /// <summary>All conditions must pass for the transition to be valid (default).</summary>
    And,
    /// <summary>Any condition passing makes the transition valid.</summary>
    Or
}

/// <summary>
/// A data resource that defines a potential transition from one state to another.
/// It bundles a path to a target state with a set of conditions that can be combined
/// using AND or OR logic. This resource is scene-independent.
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

    /// <summary>
    /// How conditions are combined. And = all must pass. Or = any must pass.
    /// Default is And to preserve backwards compatibility.
    /// </summary>
    [Export] public ConditionOperator Operator { get; private set; } = ConditionOperator.And;

    /// <summary>
    /// Evaluates all conditions using the configured operator.
    /// Returns true if conditions are empty (vacuous truth).
    /// </summary>
    /// <param name="agent">The agent node being evaluated.</param>
    /// <param name="bb">The blackboard containing state data.</param>
    /// <returns>True if conditions are met according to the operator logic.</returns>
    public bool AreConditionsMet(Node agent, IBlackboard bb)
    {
        var validConditions = Conditions.Where(c => c.IsValid());

        if (!validConditions.Any())
        {
            return true;
        }

        return Operator == ConditionOperator.And
            ? validConditions.All(c => c.Check(agent, bb))
            : validConditions.Any(c => c.Check(agent, bb));
    }

    /// <summary>
    /// Sets the conditions array. Used for testing.
    /// </summary>
    internal void SetConditions(Array<TransitionCondition> conditions)
    {
        Conditions = conditions;
    }

    /// <summary>
    /// Sets the condition operator. Used for testing.
    /// </summary>
    internal void SetOperator(ConditionOperator op)
    {
        Operator = op;
    }
}
