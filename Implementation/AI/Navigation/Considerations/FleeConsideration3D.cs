namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;

/// <summary>
/// A steering consideration that scores directions AWAY from a threat position
/// read from the blackboard. Directions aligned with the flee vector (opposite
/// to threat) score highest; directions toward the threat score zero.
///
/// Scoring formula per direction:
///   alignment = flatDir.Dot(threatDir)
///   score = max(0, -alignment) * fleeWeight
///
/// When no threat position key exists on the BB, returns all zeros (inactive).
/// </summary>
[GlobalClass, Tool]
public partial class FleeConsideration3D : BaseAIConsideration3D
{
    #region Exported Parameters

    [ExportGroup("Threat Source")]

    /// <summary>
    /// BB key for the Vector3 threat position to flee from.
    /// </summary>
    [Export]
    private StringName _threatPositionKey = new("Critter_ThreatPosition");

    [ExportGroup("Steering Behavior")]

    /// <summary>
    /// Weight of the flee consideration. Determines how strongly flee
    /// competes with other considerations (wander, wall avoidance, etc.).
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
    private float _fleeWeight = 1.2f;

    #endregion

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, _ => 0f);

        if (!blackboard.TryGet<Vector3>(_threatPositionKey, out var threatPos))
        {
            return scores;
        }

        // Calculate threat direction on XZ plane
        var toThreat = threatPos - context3D.AgentPosition;
        var flatToThreat = new Vector3(toThreat.X, 0, toThreat.Z);

        if (flatToThreat.LengthSquared() < 0.0001f)
        {
            return scores;
        }

        var threatDir = flatToThreat.Normalized();

        foreach (var dir in directions.Directions)
        {
            var flatDir = new Vector3(dir.X, 0, dir.Z);
            if (flatDir.LengthSquared() < 0.001f)
            {
                continue;
            }

            flatDir = flatDir.Normalized();
            float alignment = flatDir.Dot(threatDir);

            // Negate: directions AWAY from threat get positive scores
            if (-alignment > 0f)
            {
                scores[dir] = -alignment * _fleeWeight;
            }
        }

        return scores;
    }

    #region Test Helpers
#if TOOLS
    internal void SetThreatPositionKey(StringName key) => _threatPositionKey = key;
    internal void SetFleeWeight(float value) => _fleeWeight = value;
#endif
    #endregion
}
