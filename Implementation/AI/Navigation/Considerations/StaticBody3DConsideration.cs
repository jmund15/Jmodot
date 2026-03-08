namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using BB;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Identification;
using Core.Movement;
using Jmodot.Core.Shared.Attributes;
using Physics;
using Shared;

/// <summary>
/// A consideration that scores directions based on proximity to static bodies of a specific Category.
/// It is used to create avoidance or attraction behaviors towards environmental objects like walls,
/// cover points, or hazards.
///
/// Note: This consideration requires PropagateNegative=true on its SteeringPropagationConfig
/// so that negative avoidance scores bleed to neighboring directions for smoother steering.
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

    private bool _missingMemoryLogged;

    [ExportGroup("Distance Weighting")]
    /// <summary>
    /// Defines the range over which the consideration's weight is applied.
    /// X: The distance at which the full weight is applied (max danger/interest).
    /// Y: The distance at which the weight fades to zero.
    /// </summary>
    [Export] private Vector2 _distanceDiminishRange = new Vector2(1.0f, 5.0f);

    protected override Dictionary<Vector3, float> CalculateBaseScores(DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, dir => 0f);

        if (_targetCategory == null) { return scores; }

        if (context3D.Memory == null)
        {
            if (!_missingMemoryLogged)
            {
                JmoLogger.Error(this,
                    $"StaticBody3DConsideration requires an AIPerceptionManager3D " +
                    "but the steering context has Memory=null. The entity must pass a non-null " +
                    "AIPerceptionManager3D when constructing SteeringDecisionContext3D " +
                    "(see CritterEntity._PhysicsProcess or equivalent). " +
                    "This consideration will have NO EFFECT until Memory is provided.");
                _missingMemoryLogged = true;
            }
            return scores;
        }

        // Get all perceived objects that match our target category.
        var relevantPercepts = context3D.Memory.GetSensedByCategory(_targetCategory);

        foreach (var percept in relevantPercepts)
        {
            // Use closest surface point for accurate distance/direction on wide objects.
            // Falls back to percept origin if Target is unavailable.
            Vector3 closestPoint = percept.Target != null && GodotObject.IsInstanceValid(percept.Target)
                ? ShapeProximityCalculator.GetClosestSurfacePointOnBody(context3D.AgentPosition, percept.Target)
                : percept.LastKnownPosition;

            // Project to XZ plane for ground-based steering (matches PerceptionFleeConsideration3D)
            var toSurface = new Vector3(
                closestPoint.X - context3D.AgentPosition.X,
                0,
                closestPoint.Z - context3D.AgentPosition.Z);
            if (toSurface.LengthSquared() < 0.0001f) { continue; }

            Vector3 toTargetDir = toSurface.Normalized();
            float distance = toSurface.Length();

            float distanceWeight = GetDistanceConsideration(distance);
            if (distanceWeight <= 0f) { continue; }

            float scoreAmount = _considerationWeight * distanceWeight;

            // Find the direction in our set that best matches the direction to the surface.
            Vector3 closestDir = directions.GetClosestDirection(toTargetDir);
            scores[closestDir] += scoreAmount;
        }

        return scores;
    }

    #region Test Helpers
#if TOOLS
    internal void SetTargetCategory(Category category) => _targetCategory = category;
    internal void SetDistanceDiminishRange(Vector2 range) => _distanceDiminishRange = range;
    internal void SetConsiderationWeight(float weight) => _considerationWeight = weight;
#endif
    #endregion

    /// <summary>
    /// Calculates a weight multiplier (0.0 to 1.0) based on distance to a target.
    /// </summary>
    private float GetDistanceConsideration(float distance)
    {
        float range = _distanceDiminishRange.Y - _distanceDiminishRange.X;
        if (range <= 0f) { return 1.0f; }
        if (distance <= _distanceDiminishRange.X) { return 1.0f; }
        if (distance >= _distanceDiminishRange.Y) { return 0.0f; }

        return 1.0f - (distance - _distanceDiminishRange.X) / range;
    }
}
