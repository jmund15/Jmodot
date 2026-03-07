namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;

/// <summary>
/// Scores directions by alignment with the agent's current navigation path direction.
/// Creates the primary steering "desire" to follow a nav path toward the goal.
///
/// <para><b>Ownership:</b> This consideration is owned by <c>AISteeringProcessor3D</c> as a
/// dedicated singleton slot — NOT placed in the regular <c>_considerations</c> array.
/// This ensures exactly one nav path consideration is active at a time and prevents
/// the footgun of forgetting to include it in a BT action's considerations.</para>
///
/// <para><b>Override:</b> BT actions can temporarily swap in a custom instance via
/// <c>SteeringBehaviorAction._navPathOverride</c> for different weights, modifiers,
/// or scoring algorithms. The override is automatically cleared when the action exits.</para>
///
/// <para><b>Self-disabling:</b> Returns all-zero scores when <c>NextPathPointDirection</c>
/// is zero (no active path), so it contributes nothing during pure flee/wander states.</para>
///
/// <para><b>Extensible:</b> Attach <c>SteeringConsiderationModifier3D</c> instances
/// (e.g., <c>DistanceScalingModifier3D</c>, <c>AffinitySteeringModifier3D</c>) to the
/// <c>_modifiers</c> array for dynamic weight scaling based on distance, personality, etc.</para>
/// </summary>
[GlobalClass, Tool]
public partial class NavigationPath3DConsideration : BaseAIConsideration3D
{
    /// <summary>
    /// The weight multiplier for this consideration. A higher value will make the agent
    /// prioritize following the path above all other considerations. A lower value will
    /// allow it to be more easily distracted or influenced by its environment.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 10.0, 0.1")]
    private float _pathWeight = 1.0f;

    protected override Dictionary<Vector3, float> CalculateBaseScores(DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, dir => 0f);

        // If there is no path, this consideration has no opinion.
        if (context3D.NextPathPointDirection.IsZeroApprox())
        {
            return scores;
        }

        // Score each available direction based on how closely it aligns with the path.
        foreach (var dir in directions.Directions)
        {
            // The dot product gives us a measure of alignment (-1 to 1).
            float alignment = dir.Dot(context3D.NextPathPointDirection);

            // We only care about directions that move us generally towards the goal.
            if (alignment > 0)
            {
                scores[dir] = alignment * _pathWeight;
            }
        }

        return scores;
    }
}
