namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using BB;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Identification;
using Core.Movement;
using Shared;

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
    [Export] private Category _targetCategory;

    [ExportGroup("Distance Weighting")]
    /// <summary>
    /// Defines the range over which the consideration's weight is applied.
    /// X: The distance at which the full weight is applied (max danger/interest).
    /// Y: The distance at which the weight fades to zero.
    /// </summary>
    [Export] private Vector2 _distanceDiminishRange = new Vector2(1.0f, 5.0f);

    [ExportGroup("Score Propagation")]
    [Export] private bool _propagateScores = true;
    [Export(PropertyHint.Range, "1, 8, 1")] private int _dirsToPropagate = 2;
    [Export(PropertyHint.Range, "0.1, 0.9, 0.05")] private float _propDiminishWeight = 0.5f;

    private List<Vector3> _orderedDirections;
    private Map<int, Vector3> _dirIds = new Map<int, Vector3>();

    public override void Initialize(DirectionSet3D directions)
    {
        _orderedDirections = directions.Directions.ToList();
        _dirIds.Clear();
        for (int i = 0; i < _orderedDirections.Count; i++)
        {
            _dirIds.Add(i, _orderedDirections[i]);
        }
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
            Propagate(ref scores);
        }

        return scores;
    }

    public float GetDistanceConsiderationKexponential(float detectDist)
    {
        if (detectDist > this._distanceDiminishRange.Y)
        {
            return 0f;
        }

        // the closer the collision is to the raycast, the higher the "danger" weight
        var minWeight = 0.1f;
        var k = 2.5f;
        float distWeight;

        if (detectDist <= this._distanceDiminishRange.X)
        {
            distWeight = 1.0f; // Ensure max weight
        }
        else
        {
            distWeight = 1f - (detectDist - this._distanceDiminishRange.X) /
                (this._distanceDiminishRange.Y - this._distanceDiminishRange.X);
        }

        //distWeight = minWeight + (1.0f - minWeight) *
        //    (float)Math.Exp(-k * (collDist - _distDiminishRange.X) / (_distDiminishRange.Y/*castLength*/ - _distDiminishRange.X));
        distWeight = Mathf.Clamp(distWeight, 0f, 1f);
        //GD.Print($"{raycast.TargetPosition.Normalized().GetDir16()}'s wall dist: {collDist}\ndistWeight: {distWeight}");
        return distWeight;
    }
    /// <summary>
    /// Calculates a weight multiplier (0.0 to 1.0) based on distance to a target.
    /// </summary>
    private float GetDistanceConsideration(float distance)
    {
        if (distance <= _distanceDiminishRange.X) { return 1.0f; } // Max weight
        if (distance >= _distanceDiminishRange.Y) { return 0.0f; } // No weight

        // Linearly interpolate the weight between the min and max distances.
        return 1.0f - (distance - _distanceDiminishRange.X) / (_distanceDiminishRange.Y - _distanceDiminishRange.X);
    }

    /// <summary>
    /// Propagates scores to neighboring directions to create a smoother AI response.
    /// </summary>
    private void Propagate(ref Dictionary<Vector3, float> scores)
    {
        if (_orderedDirections == null || _orderedDirections.Count == 0) { return; }

        var initialScores = new Dictionary<Vector3, float>(scores);
        int dirCount = _orderedDirections.Count;

        for (int i = 0; i < dirCount; i++)
        {
            int dirId = _dirIds.Reverse[_orderedDirections[i]];
            float initialScore = initialScores[_orderedDirections[i]];
            if (initialScore == 0f) { continue; }

            float propWeight = initialScore * _propDiminishWeight;

            for (int j = 1; j <= _dirsToPropagate; j++)
            {
                int leftIndex = (dirId - j + dirCount) % dirCount;
                scores[_dirIds.Forward[leftIndex]] += propWeight;

                int rightIndex = (dirId + j) % dirCount;
                scores[_dirIds.Forward[rightIndex]] += propWeight;

                propWeight *= _propDiminishWeight;
            }
        }
    }
}
