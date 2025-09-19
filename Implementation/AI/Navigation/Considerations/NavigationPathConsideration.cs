namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;

/// <summary>
/// A consideration that scores directions based on their alignment with the agent's
/// current navigation path from the AINavigator3D. This creates the primary "desire"
/// to move towards the goal. The weight can be tuned to make an agent follow its
/// path more or less strictly.
/// </summary>
/// <summary>
///     The path that defines any weight or modifiers for the base navigation path to the target.
///     This is typically optional, but if you want certain entities to regard the path strictly or less of a priority
///     based on certain conditions, use this.
/// </summary>
[GlobalClass]
public partial class NavigationPathConsideration : BaseAIConsideration3D
{
    /// <summary>
    /// The weight multiplier for this consideration. A higher value will make the agent
    /// prioritize following the path above all other considerations. A lower value will
    /// allow it to be more easily distracted or influenced by its environment.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 10.0, 0.1")]
    private float _pathWeight = 1.0f;

    protected override Dictionary<Vector3, float> CalculateBaseScores(DirectionSet3D directions, SteeringDecisionContext context, IBlackboard blackboard)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, dir => 0f);

        // If there is no path, this consideration has no opinion.
        if (context.NextPathPointDirection.IsZeroApprox())
        {
            return scores;
        }

        // Score each available direction based on how closely it aligns with the path.
        foreach (var dir in directions.Directions)
        {
            // The dot product gives us a measure of alignment (-1 to 1).
            float alignment = dir.Dot(context.NextPathPointDirection);

            // We only care about directions that move us generally towards the goal.
            if (alignment > 0)
            {
                scores[dir] = alignment * _pathWeight;
            }
        }

        return scores;
    }
}
