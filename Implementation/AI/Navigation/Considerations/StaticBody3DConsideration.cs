namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using BB;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Identification;
using Core.Movement;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// A consideration that scores directions based on proximity to static bodies of a specific Category.
/// It is used to create avoidance or attraction behaviors towards environmental objects like walls,
/// cover points, or hazards.
/// </summary>
[GlobalClass]
public partial class StaticBody3DConsideration : BaseAIConsideration3D
{
    [ExportGroup("Behavior Tuning")]
    /// <summary>
    /// The core weight of this consideration. Negative values create avoidance (danger),
    /// while positive values create attraction (interest).
    /// </summary>
    [Export(PropertyHint.Range, "-5.0, 5.0, 0.1")]
    private float _considerationWeight = -1.0f; // Default to avoidance

    /// <summary>
    /// The consideration will only react to perceived objects whose Identity belongs to this Category.
    /// </summary>
    [Export, RequiredExport] private Category _targetCategory = null!;

    [ExportGroup("Distance Weighting")]
    /// <summary>
    /// Defines the range over which the consideration's weight is applied.
    /// X: The distance at which the full weight is applied (max danger/interest).
    /// Y: The distance at which the weight fades to zero.
    /// </summary>
    [Export] private Vector2 _distanceDiminishRange = new Vector2(1.0f, 5.0f);

    [ExportGroup("Score Propagation")]
    /// <summary>
    /// If enabled, the calculated score for the closest direction will be "bled" to its
    /// neighboring directions, creating a smoother, less jerky avoidance response.
    /// </summary>
    [Export] private bool _propagateScores = true;

    /// <summary>
    /// The number of neighboring directions on each side to propagate the score to.
    /// </summary>
    [Export(PropertyHint.Range, "1, 8, 1")] private int _dirsToPropagate = 2;

    /// <summary>
    /// The multiplier applied to the score for each step of propagation. A value of 0.5
    /// means the first neighbor gets 50% of the original score, the second gets 25%, etc.
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 0.9, 0.05")] private float _propDiminishWeight = 0.5f;

    private List<Vector3> _orderedDirections = null!;

    public override void Initialize(DirectionSet3D directions)
    {
        _orderedDirections = directions.Directions.ToList();
    }

    protected override Dictionary<Vector3, float> CalculateBaseScores(DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, dir => 0f);

        if (_targetCategory == null) { return scores; }

        // Get all perceived objects that match our target category.
        var relevantPercepts = context3D.Memory.GetSensedByCategory(_targetCategory);

        foreach (var percept in relevantPercepts)
        {
            Vector3 toTargetDir = (percept.LastKnownPosition - context3D.AgentPosition).Normalized();
            float distance = context3D.AgentPosition.DistanceTo(percept.LastKnownPosition);

            // Calculate the weight based on distance.
            float distanceWeight = GetDistanceConsideration(distance);
            if (distanceWeight <= 0f) { continue; }

            float scoreAmount = _considerationWeight * distanceWeight;

            // Find the direction in our set that best matches the direction to the object.
            Vector3 closestDir = directions.GetClosestDirection(toTargetDir);
            scores[closestDir] += scoreAmount;
        }

        if (_propagateScores)
        {
            SteeringPropagation.PropagateScores(scores, _orderedDirections, _dirsToPropagate,
                _propDiminishWeight, propagateNegative: true);
        }

        return scores;
    }

    /// <summary>
    /// Calculates a weight multiplier (0.0 to 1.0) based on distance to a target.
    /// </summary>
    private float GetDistanceConsideration(float distance)
    {
        if (distance <= _distanceDiminishRange.X) { return 1.0f; }
        if (distance >= _distanceDiminishRange.Y) { return 0.0f; }

        return 1.0f - (distance - _distanceDiminishRange.X) / (_distanceDiminishRange.Y - _distanceDiminishRange.X);
    }
}
