namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;

/// <summary>
/// Soft spatial containment tied to a zone defined on the blackboard.
/// As the agent approaches the zone boundary, outward-facing directions
/// progressively lose interest — creating a natural "soft leash" that
/// curves the agent back toward the center.
///
/// Zone data is stored on BB as Vector4(centerX, centerY, centerZ, radius).
/// This decouples the consideration from Godot physics (Area3D/CollisionShape3D)
/// and keeps the core math testable.
///
/// Sphere shape only (v1). Box support can be added by extending the
/// distance calculation to use per-axis extents.
/// </summary>
[GlobalClass]
public partial class ZoneBoundaryConsideration3D : BaseAIConsideration3D
{
    #region Exported Parameters

    [ExportGroup("Zone Configuration")]

    /// <summary>
    /// BB key for the zone data. Expected type: Vector4(centerX, centerY, centerZ, radius).
    /// </summary>
    [Export]
    private StringName? _boundaryZoneKey;

    [ExportGroup("Penalty Behavior")]

    /// <summary>
    /// Maximum penalty weight applied at the zone boundary edge.
    /// Higher values create stronger containment.
    /// </summary>
    [Export(PropertyHint.Range, "0.5, 5.0, 0.1")]
    private float _penaltyWeight = 2.0f;

    /// <summary>
    /// Normalized distance (0-1) where the penalty falloff begins.
    /// 0.7 means penalty starts at 70% of the way from center to edge.
    /// Lower values create earlier, gentler containment.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _falloffStartNormalized = 0.7f;

    /// <summary>
    /// Optional curve for non-linear falloff. X axis = 0 (at falloff start) to 1 (at edge).
    /// Y axis = penalty multiplier (0-1). Null = linear falloff.
    /// </summary>
    [Export]
    private Curve? _falloffCurve;

    #endregion

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, _ => 0f);

        // Read zone data from BB
        if (_boundaryZoneKey == null ||
            !blackboard.TryGet<Vector4>(_boundaryZoneKey, out var zoneData))
        {
            return scores;
        }

        Vector3 zoneCenter = new Vector3(zoneData.X, zoneData.Y, zoneData.Z);
        float radius = zoneData.W;

        if (radius <= 0f)
        {
            return scores;
        }

        // Calculate how far agent is from center (XZ plane, normalized)
        float normalizedDist = CalculateNormalizedDistanceFromCenter(
            context3D.AgentPosition, zoneCenter, radius);

        // Calculate penalty strength based on distance
        float penaltyStrength = CalculateBoundaryPenalty(
            normalizedDist, _falloffStartNormalized, _falloffCurve);

        if (penaltyStrength <= 0f)
        {
            return scores;
        }

        // Direction toward center (XZ plane)
        Vector3 toCenter = zoneCenter - context3D.AgentPosition;
        toCenter.Y = 0;
        if (toCenter.LengthSquared() < 0.001f)
        {
            return scores;
        }
        Vector3 towardCenter = toCenter.Normalized();

        // Score each direction
        foreach (var dir in directions.Directions)
        {
            Vector3 flatDir = new Vector3(dir.X, 0, dir.Z);
            if (flatDir.LengthSquared() < 0.001f)
            {
                continue;
            }
            flatDir = flatDir.Normalized();

            scores[dir] = CalculateDirectionPenalty(
                flatDir, towardCenter, penaltyStrength, _penaltyWeight);
        }

        return scores;
    }

    /// <summary>
    /// Calculates the agent's normalized distance from zone center on the XZ plane.
    /// 0 = at center, 1 = at edge, >1 = outside zone.
    /// Y axis is ignored for ground-based containment.
    /// </summary>
    public static float CalculateNormalizedDistanceFromCenter(
        Vector3 agentPos, Vector3 zoneCenter, float radius)
    {
        Vector3 offset = agentPos - zoneCenter;
        offset.Y = 0; // XZ plane only
        return offset.Length() / radius;
    }

    /// <summary>
    /// Calculates the penalty strength (0-1) based on normalized distance.
    /// Returns 0 inside the safe zone (before falloffStart).
    /// Returns 0-1 in the falloff zone (linear or curve-shaped).
    /// Clamped to 1 at or beyond the edge.
    /// </summary>
    public static float CalculateBoundaryPenalty(
        float normalizedDistance, float falloffStart, Curve? curve)
    {
        if (normalizedDistance <= falloffStart)
        {
            return 0f;
        }

        // Map distance from [falloffStart, 1.0] to [0, 1]
        float falloffRange = 1f - falloffStart;
        if (falloffRange <= 0f)
        {
            return normalizedDistance >= 1f ? 1f : 0f;
        }

        float t = Mathf.Clamp((normalizedDistance - falloffStart) / falloffRange, 0f, 1f);

        // Apply curve if provided
        if (curve != null)
        {
            t = curve.Sample(t);
        }

        return t;
    }

    /// <summary>
    /// Calculates the score adjustment for a single direction based on its alignment
    /// with the center direction. Directions pointing away from center get negative
    /// scores. Directions toward center are unaffected (zero). Perpendicular directions
    /// get zero.
    /// </summary>
    public static float CalculateDirectionPenalty(
        Vector3 direction, Vector3 towardCenter, float penaltyStrength, float weight)
    {
        if (penaltyStrength <= 0f)
        {
            return 0f;
        }

        // Dot product: +1 = toward center, -1 = away from center, 0 = perpendicular
        float alignment = direction.Dot(towardCenter);

        // Only penalize directions pointing AWAY from center (negative alignment)
        if (alignment >= 0f)
        {
            return 0f;
        }

        // alignment is negative, so this produces a negative score (penalty)
        return alignment * penaltyStrength * weight;
    }

    #region Test Helpers
#if TOOLS
    internal void SetBoundaryZoneKey(StringName key) => _boundaryZoneKey = key;
    internal void SetPenaltyWeight(float value) => _penaltyWeight = value;
    internal void SetFalloffStart(float value) => _falloffStartNormalized = value;
    internal void SetFalloffCurve(Curve? curve) => _falloffCurve = curve;
#endif
    #endregion
}
