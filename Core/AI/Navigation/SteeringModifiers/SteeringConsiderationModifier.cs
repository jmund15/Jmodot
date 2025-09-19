namespace Jmodot.Core.AI.Navigation.SteeringModifiers;

using System.Collections.Generic;
using BB;
using Implementation.AI.Navigation;

/// <summary>
///     An abstract resource that modifies the directional scores produced by a steering consideration.
///     This allows an AI's personality (Affinities) to influence its low-level movement behavior.
/// </summary>
[GlobalClass]
public abstract partial class SteeringConsiderationModifier : Resource
{
    /// <summary>
    ///     Modifies the dictionary of steering scores.
    /// </summary>
    /// <param name="scores">The current dictionary of directional scores to be modified.</param>
    /// <param name="context">The per-frame snapshot of the AI's state and world view.</param>
    /// <param name="blackboard">The AI's blackboard for accessing core components.</param>
    public abstract void Modify(ref Dictionary<Vector3, float> scores, SteeringDecisionContext context, IBlackboard blackboard);
}
