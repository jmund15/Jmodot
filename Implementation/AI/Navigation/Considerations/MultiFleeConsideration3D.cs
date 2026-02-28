namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Shared;

/// <summary>
/// A steering consideration that computes combined repulsion from multiple threat
/// positions read as Vector3[] from the blackboard. Unlike FleeConsideration3D
/// (single threat), this handles N threats simultaneously.
///
/// The combined flee vector is the average of individual "away" vectors for each threat.
/// When threats surround the agent from opposing sides, the flee vectors cancel out,
/// producing zero scores in all directions — the "cornered" state.
///
/// Scoring:
///   foreach threat: combinedFlee -= (threatPos - agentPos).Normalized()
///   if combinedFlee ≈ zero: return all zeros (cornered)
///   else: score = max(0, direction.Dot(combinedFlee.Normalized())) * fleeWeight
/// </summary>
[GlobalClass, Tool]
public partial class MultiFleeConsideration3D : BaseAIConsideration3D
{
    #region Exported Parameters

    [ExportGroup("Threat Source")]

    /// <summary>
    /// BB key for the Vector3[] array of all active threat positions.
    /// Updated each physics frame by ThreatDetectionZone.
    /// </summary>
    [Export]
    private StringName _threatPositionsKey = new("Critter_ThreatPositions");

    [ExportGroup("Steering Behavior")]

    /// <summary>
    /// Weight of the flee consideration. Higher values make flee dominate
    /// over other considerations (wander, wall avoidance).
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
    private float _fleeWeight = 1.5f;

    [ExportGroup("Score Propagation")]

    [Export]
    private bool _propagateScores = true;

    [Export(PropertyHint.Range, "1, 4, 1")]
    private int _dirsToPropagate = 2;

    [Export(PropertyHint.Range, "0.1, 0.9, 0.05")]
    private float _propDiminishWeight = 0.5f;

    #endregion

    private List<Vector3>? _orderedDirections;

    public override void Initialize(DirectionSet3D directions)
    {
        _orderedDirections = directions.Directions.ToList();
    }

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, _ => 0f);

        if (!blackboard.TryGet<Vector3[]>(_threatPositionsKey, out var threatPositions))
        {
            return scores;
        }

        if (threatPositions == null || threatPositions.Length == 0)
        {
            return scores;
        }

        // Calculate combined flee direction from all threats
        var combinedFlee = CalculateCombinedFleeDirection(
            context3D.AgentPosition, threatPositions);

        if (combinedFlee.LengthSquared() < 0.0001f)
        {
            return scores;
        }

        var fleeDir = combinedFlee.Normalized();

        // Score each direction by alignment with combined flee direction
        foreach (var dir in directions.Directions)
        {
            var flatDir = new Vector3(dir.X, 0, dir.Z);
            if (flatDir.LengthSquared() < 0.001f)
            {
                continue;
            }

            flatDir = flatDir.Normalized();
            float alignment = flatDir.Dot(fleeDir);

            if (alignment > 0f)
            {
                scores[dir] = alignment * _fleeWeight;
            }
        }

        if (_propagateScores && _orderedDirections != null)
        {
            SteeringPropagation.PropagateScores(
                scores, _orderedDirections, _dirsToPropagate, _propDiminishWeight);
        }

        return scores;
    }

    /// <summary>
    /// Computes the combined flee direction on the XZ plane from all threat positions.
    /// Each threat contributes an equal "push away" vector. When threats are on
    /// opposing sides, their push vectors cancel out → zero length = cornered.
    /// </summary>
    public static Vector3 CalculateCombinedFleeDirection(
        Vector3 agentPosition, Vector3[] threatPositions)
    {
        var combined = Vector3.Zero;

        foreach (var threatPos in threatPositions)
        {
            var toThreat = threatPos - agentPosition;
            var flat = new Vector3(toThreat.X, 0, toThreat.Z);

            if (flat.LengthSquared() < 0.0001f)
            {
                continue;
            }

            // Away from this threat (negate the direction toward it)
            combined -= flat.Normalized();
        }

        return combined;
    }

    #region Test Helpers
#if TOOLS
    internal void SetThreatPositionsKey(StringName key) => _threatPositionsKey = key;
    internal void SetFleeWeight(float value) => _fleeWeight = value;
    internal void SetPropagateScores(bool value) => _propagateScores = value;
#endif
    #endregion
}
