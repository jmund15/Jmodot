namespace Jmodot.Core.AI.Navigation.Considerations;

using System.Collections.Generic;
using BB;
using Implementation.AI.Navigation;
using Movement;
using SteeringModifiers;
using GColl = Godot.Collections;

/// <summary>
/// The abstract base class for any environmental consideration. Its purpose is to
/// evaluate the current world state (via the SteeringDecisionContext) and produce a
/// dictionary of scores, where each key is a direction and each value is a float
/// representing the "interest" or "danger" associated with moving in that direction.
/// </summary>
[GlobalClass, Tool]
public abstract partial class BaseAIConsideration3D : Resource
{
    /// <summary>
    /// The evaluation priority of this consideration. While all considerations are summed
    /// together, this can be used for debugging or for systems that may need a specific order.
    /// </summary>
    [Export] public int Priority { get; protected set; } = 1;

    /// <summary>
    /// A list of modifiers that can alter the objective scores of this consideration,
    /// allowing an AI's personality (Affinities) to influence its low-level behavior.
    /// </summary>
    [Export] private GColl.Array<SteeringConsiderationModifier3D> _modifiers = new();

    /// <summary>
    /// Called once by the AISteeringProcessor during initialization. This allows the
    /// consideration to perform any necessary setup or caching.
    /// </summary>
    /// <param name="directions">The DirectionSet3D used by the agent.</param>
    public virtual void Initialize(DirectionSet3D directions) { }

    /// <summary>
    /// The primary evaluation method. It calculates the base scores and then allows
    /// all registered modifiers to alter them before adding to the final scores.
    /// </summary>
    /// <param name="context3D">A snapshot of the current world and agent state.</param>
    /// <param name="blackboard">The AI's blackboard for accessing core components.</param>
    /// <param name="directions">The set of directions to score.</param>
    /// <param name="finalScores">The master score dictionary from the steering processor, passed by reference.</param>
    public void Evaluate(SteeringDecisionContext3D context3D, IBlackboard blackboard,
        DirectionSet3D directions, ref Dictionary<Vector3, float> finalScores)
    {
        // 1. Calculate the raw, objective scores for this consideration.
        var baseScores = CalculateBaseScores(directions, context3D, blackboard);

        // 2. Apply all subjective modifiers to the base scores.
        foreach (var modifier in _modifiers)
        {
            modifier.Modify(ref baseScores, context3D, blackboard);
        }

        // 3. Add the final, modified scores to the processor's master score dictionary.
        foreach (var score in baseScores)
        {
            if (finalScores.ContainsKey(score.Key))
            {
                finalScores[score.Key] += score.Value;
            }
        }
    }

    /// <summary>
    /// Child classes MUST implement this method. It contains the core logic for calculating
    /// the raw directional scores before any personality-driven modifications are applied.
    /// </summary>
    protected abstract Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard);
}
