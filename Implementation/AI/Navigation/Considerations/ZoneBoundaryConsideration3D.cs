namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.AI.Navigation.Zones;
using Core.Movement;

/// <summary>
/// Soft spatial containment that penalizes outward-facing directions near zone boundaries.
/// Creates a natural "soft leash" — the agent curves back toward the interior without
/// hitting an invisible wall.
///
/// Two zone sourcing modes:
///
/// 1. Shape-based (recommended): Set _zoneShape to a ZoneShape3D resource (e.g., SphereZoneShape3D).
///    Zone center is either self-managed (captured from agent's first evaluation position)
///    or BB-sourced (when _boundaryZoneKey is set). Shape handles geometry math.
///
/// 2. Legacy BB path: When _zoneShape is null and _boundaryZoneKey is set, reads
///    Vector4(centerX, centerY, centerZ, radius) from BB. Inline sphere math.
///    Preserved for backward compatibility.
/// </summary>
[GlobalClass, Tool]
public partial class ZoneBoundaryConsideration3D : BaseAIConsideration3D
{
    #region Exported Parameters

    [ExportGroup("Zone Shape")]

    /// <summary>
    /// Zone geometry definition (sphere, box, etc.). Handles normalized distance
    /// and direction-to-interior calculations for the configured shape.
    /// When set: uses shape-based evaluation with self-managed or BB center.
    /// When null: falls back to legacy Vector4 BB path.
    /// </summary>
    [Export]
    private ZoneShape3D? _zoneShape;

    [ExportGroup("Zone Center")]

    /// <summary>
    /// Optional BB key for externally-managed zone center.
    /// When null (default): center auto-captured from agent's first evaluation position.
    /// When set: center read from BB as Vector4(centerX, centerY, centerZ, *).
    /// Also enables legacy Vector4 path when _zoneShape is null.
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

    /// <summary>
    /// Zone center captured on first evaluation (self-managed mode only).
    /// Persists across BT register/unregister cycles for "home base" behavior.
    /// Reset via ResetSelfZone() for redeployment scenarios.
    /// </summary>
    private Vector3? _selfZoneCenter;

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, _ => 0f);

        if (_zoneShape != null)
        {
            return CalculateShapeBasedScores(scores, directions, context3D, blackboard);
        }

        if (_boundaryZoneKey != null)
        {
            return CalculateLegacyBBScores(scores, directions, context3D, blackboard);
        }

        return scores;
    }

    /// <summary>
    /// Shape-based evaluation path. Uses ZoneShape3D for geometry math.
    /// Center sourced from self-managed capture or BB.
    /// </summary>
    private Dictionary<Vector3, float> CalculateShapeBasedScores(
        Dictionary<Vector3, float> scores,
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard)
    {
        if (!TryResolveCenter(context3D.AgentPosition, blackboard, out var zoneCenter))
        {
            return scores;
        }

        float normalizedDist = _zoneShape!.GetNormalizedDistance(context3D.AgentPosition, zoneCenter);
        float penaltyStrength = CalculateBoundaryPenalty(normalizedDist, _falloffStartNormalized, _falloffCurve);

        if (penaltyStrength <= 0f)
        {
            return scores;
        }

        Vector3 towardInterior = _zoneShape.GetDirectionToInterior(context3D.AgentPosition, zoneCenter);
        ApplyBoundaryPenalties(scores, directions, towardInterior, penaltyStrength);
        return scores;
    }

    /// <summary>
    /// Legacy BB path. Reads Vector4(centerX, centerY, centerZ, radius) from BB.
    /// Inline sphere math for backward compatibility.
    /// </summary>
    private Dictionary<Vector3, float> CalculateLegacyBBScores(
        Dictionary<Vector3, float> scores,
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard)
    {
        if (!blackboard.TryGet<Vector4>(_boundaryZoneKey!, out var zoneData))
        {
            return scores;
        }

        Vector3 zoneCenter = new Vector3(zoneData.X, zoneData.Y, zoneData.Z);
        float radius = zoneData.W;

        if (radius <= 0f)
        {
            return scores;
        }

        float normalizedDist = CalculateNormalizedDistanceFromCenter(
            context3D.AgentPosition, zoneCenter, radius);
        float penaltyStrength = CalculateBoundaryPenalty(
            normalizedDist, _falloffStartNormalized, _falloffCurve);

        if (penaltyStrength <= 0f)
        {
            return scores;
        }

        Vector3 toCenter = zoneCenter - context3D.AgentPosition;
        toCenter.Y = 0;
        if (toCenter.LengthSquared() < 0.001f)
        {
            return scores;
        }

        ApplyBoundaryPenalties(scores, directions, toCenter.Normalized(), penaltyStrength);
        return scores;
    }

    /// <summary>
    /// Resolves the zone center from self-managed state or BB.
    /// </summary>
    private bool TryResolveCenter(Vector3 agentPosition, IBlackboard blackboard, out Vector3 center)
    {
        if (_boundaryZoneKey != null)
        {
            // BB-sourced center (for shared/external zones)
            if (!blackboard.TryGet<Vector4>(_boundaryZoneKey, out var zoneData))
            {
                center = Vector3.Zero;
                return false;
            }
            center = new Vector3(zoneData.X, zoneData.Y, zoneData.Z);
            return true;
        }

        // Self-managed center: capture agent position on first evaluation
        _selfZoneCenter ??= agentPosition;
        center = _selfZoneCenter.Value;
        return true;
    }

    /// <summary>
    /// Applies boundary penalties to all directions based on their alignment with
    /// the toward-interior vector. Shared between shape-based and legacy paths.
    /// </summary>
    private void ApplyBoundaryPenalties(
        Dictionary<Vector3, float> scores,
        DirectionSet3D directions,
        Vector3 towardInterior,
        float penaltyStrength)
    {
        foreach (var dir in directions.Directions)
        {
            Vector3 flatDir = new Vector3(dir.X, 0, dir.Z);
            if (flatDir.LengthSquared() < 0.001f)
            {
                continue;
            }
            flatDir = flatDir.Normalized();

            scores[dir] = CalculateDirectionPenalty(
                flatDir, towardInterior, penaltyStrength, _penaltyWeight);
        }
    }

    /// <summary>
    /// Clears the self-managed zone center. Next evaluation re-captures agent position.
    /// Call when redeploying a pooled critter to a new location.
    /// </summary>
    public void ResetSelfZone() => _selfZoneCenter = null;

    #region Static Math (Shared)

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
    /// scores. Directions toward center are unaffected (zero).
    /// </summary>
    public static float CalculateDirectionPenalty(
        Vector3 direction, Vector3 towardCenter, float penaltyStrength, float weight)
    {
        if (penaltyStrength <= 0f)
        {
            return 0f;
        }

        float alignment = direction.Dot(towardCenter);

        if (alignment >= 0f)
        {
            return 0f;
        }

        return alignment * penaltyStrength * weight;
    }

    #endregion

    #region Test Helpers
#if TOOLS
    internal void SetBoundaryZoneKey(StringName key) => _boundaryZoneKey = key;
    internal void SetPenaltyWeight(float value) => _penaltyWeight = value;
    internal void SetFalloffStart(float value) => _falloffStartNormalized = value;
    internal void SetFalloffCurve(Curve? curve) => _falloffCurve = curve;
    internal void SetZoneShape(ZoneShape3D? shape) => _zoneShape = shape;
#endif
    #endregion
}
