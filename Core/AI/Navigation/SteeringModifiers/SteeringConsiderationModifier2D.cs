namespace Jmodot.Core.AI.Navigation.SteeringModifiers;

using System.Collections.Generic;
using BB;
using Implementation.AI.Navigation;

/// <summary>
///     An abstract resource that modifies the directional scores produced by a steering consideration.
///     This allows an AI's personality (Affinities) to influence its low-level movement behavior.
/// </summary>
[GlobalClass]
public abstract partial class SteeringConsiderationModifier2D : Resource
{
    /// <summary>
    ///     Modifies the dictionary of steering scores.
    /// </summary>
    /// <param name="scores">The current dictionary of directional scores to be modified.</param>
    /// <param name="context3D">The per-frame snapshot of the AI's state and world view.</param>
    /// <param name="blackboard">The AI's blackboard for accessing core components.</param>
    public abstract void Modify(ref Dictionary<Vector2, float> scores, SteeringDecisionContext2D context, IBlackboard blackboard);
}
