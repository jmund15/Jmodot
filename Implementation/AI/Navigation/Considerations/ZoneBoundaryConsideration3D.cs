namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.AI.Navigation.Zones;
using Core.Movement;

/// <summary>
/// Dual-mode spatial containment that scores directions near zone boundaries.
///
/// Two independent scoring axes:
/// - Penalty (repulsion): negative scores for outward-facing directions near the edge.
///   Creates a natural "soft leash." Includes rubber-band return when outside the zone.
/// - Attraction (pull): positive scores for inward-facing directions.
///   Ensures the agent always has a viable direction, preventing stalls.
///
/// Both modes are independently configurable with weight, ramp range, and optional curve.
/// Default config (attraction weight = 0) is fully backward compatible.
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

    [ExportGroup("Penalty (Away-From-Center Repulsion)")]

    /// <summary>
    /// Maximum penalty score magnitude at full ramp strength and perfect alignment.
    /// Score = alignment × rampStrength × penaltyMaxWeight (always negative).
    /// 0 disables penalty entirely.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 5.0, 0.1")]
    private float _penaltyMaxWeight = 1.0f;

    /// <summary>
    /// Normalized distance where the penalty ramp begins.
    /// 0.7 means penalty starts at 70% of the way from center to edge.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 2.0, 0.05")]
    private float _penaltyRampStart = 0.7f;

    /// <summary>
    /// Normalized distance where the penalty ramp reaches full strength.
    /// 1.0 = at zone edge (default, backward compatible).
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 2.0, 0.05")]
    private float _penaltyRampEnd = 1.0f;

    /// <summary>
    /// Optional curve for non-linear penalty ramp. X = 0 (at ramp start) to 1 (at ramp end).
    /// Y = penalty multiplier (0-1). Null = linear ramp.
    /// </summary>
    [Export]
    private Curve? _penaltyCurve;

    [ExportGroup("Attraction (Toward-Center Pull)")]

    /// <summary>
    /// Maximum positive score for inward-facing directions at full ramp strength.
    /// Score = alignment × rampStrength × attractionMaxWeight (always positive).
    /// 0 disables attraction (backward compatible default).
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 5.0, 0.1")]
    private float _attractionMaxWeight = 1.0f;

    /// <summary>
    /// Normalized distance where attraction begins.
    /// 1.0 = outside zone only (default). 0.0 = attraction everywhere.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 2.0, 0.05")]
    private float _attractionRampStart = 1.0f;

    /// <summary>
    /// Normalized distance where attraction reaches full strength.
    /// 1.3 = full attraction at 130% of zone radius.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 2.0, 0.05")]
    private float _attractionRampEnd = 1.3f;

    /// <summary>
    /// Optional curve for non-linear attraction ramp. X = 0 (at ramp start) to 1 (at ramp end).
    /// Y = attraction multiplier (0-1). Null = linear ramp.
    /// </summary>
    [Export]
    private Curve? _attractionCurve;

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

        if (!string.IsNullOrEmpty(_boundaryZoneKey))
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
        float penaltyStrength = CalculateRampStrength(
            normalizedDist, _penaltyRampStart, _penaltyRampEnd, _penaltyCurve);
        float attractionStrength = CalculateRampStrength(
            normalizedDist, _attractionRampStart, _attractionRampEnd, _attractionCurve);

        if (penaltyStrength <= 0f && attractionStrength <= 0f)
        {
            return scores;
        }

        Vector3 towardInterior = _zoneShape.GetDirectionToInterior(context3D.AgentPosition, zoneCenter);
        ApplyBoundaryScores(scores, directions, towardInterior,
            penaltyStrength, attractionStrength, normalizedDist);
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
        float penaltyStrength = CalculateRampStrength(
            normalizedDist, _penaltyRampStart, _penaltyRampEnd, _penaltyCurve);
        float attractionStrength = CalculateRampStrength(
            normalizedDist, _attractionRampStart, _attractionRampEnd, _attractionCurve);

        if (penaltyStrength <= 0f && attractionStrength <= 0f)
        {
            return scores;
        }

        Vector3 toCenter = zoneCenter - context3D.AgentPosition;
        toCenter.Y = 0;
        if (toCenter.LengthSquared() < 0.001f)
        {
            return scores;
        }

        ApplyBoundaryScores(scores, directions, toCenter.Normalized(),
            penaltyStrength, attractionStrength, normalizedDist);
        return scores;
    }

    /// <summary>
    /// Resolves the zone center from self-managed state or BB.
    /// </summary>
    private bool TryResolveCenter(Vector3 agentPosition, IBlackboard blackboard, out Vector3 center)
    {
        if (!string.IsNullOrEmpty(_boundaryZoneKey))
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
    /// Applies both penalty and attraction scores to all directions.
    /// Penalty: negative scores for outward-facing directions (existing rubber-band behavior).
    /// Attraction: positive scores for inward-facing directions (new).
    /// Both are independently weighted and ramped.
    /// </summary>
    private void ApplyBoundaryScores(
        Dictionary<Vector3, float> scores,
        DirectionSet3D directions,
        Vector3 towardInterior,
        float penaltyStrength,
        float attractionStrength,
        float normalizedDistance)
    {
        foreach (var dir in directions.Directions)
        {
            Vector3 flatDir = new Vector3(dir.X, 0, dir.Z);
            if (flatDir.LengthSquared() < 0.001f)
            {
                continue;
            }
            flatDir = flatDir.Normalized();

            // Penalty pass: outward directions get negative scores
            if (penaltyStrength > 0f)
            {
                scores[dir] += CalculateDirectionPenalty(
                    flatDir, towardInterior, penaltyStrength, _penaltyMaxWeight, normalizedDistance);
            }

            // Attraction pass: inward directions get positive scores
            if (attractionStrength > 0f && _attractionMaxWeight > 0f)
            {
                float alignment = flatDir.Dot(towardInterior);
                if (alignment > 0f)
                {
                    scores[dir] += alignment * attractionStrength * _attractionMaxWeight;
                }
            }
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
    /// Generalized ramp function. Returns 0 before rampStart, ramps 0→1 between
    /// rampStart and rampEnd, clamps at 1 beyond rampEnd. Optional curve for non-linearity.
    /// Used by both penalty and attraction systems.
    /// </summary>
    public static float CalculateRampStrength(
        float normalizedDistance, float rampStart, float rampEnd, Curve? curve)
    {
        if (normalizedDistance < rampStart)
        {
            return 0f;
        }

        float rampRange = rampEnd - rampStart;
        if (rampRange <= 0f)
        {
            return normalizedDistance >= rampStart ? 1f : 0f;
        }

        float t = Mathf.Clamp((normalizedDistance - rampStart) / rampRange, 0f, 1f);

        if (curve != null)
        {
            t = curve.Sample(t);
        }

        return t;
    }

    /// <summary>
    /// Backward-compatible wrapper. Equivalent to CalculateRampStrength with rampEnd=1.0.
    /// </summary>
    public static float CalculateBoundaryPenalty(
        float normalizedDistance, float falloffStart, Curve? curve)
    {
        return CalculateRampStrength(normalizedDistance, falloffStart, 1.0f, curve);
    }

    /// <summary>
    /// How far outside the zone (as fraction of zone radius) it takes to reach
    /// full return strictness. 0.3 = full strictness at 130% radius.
    /// </summary>
    private const float ReturnTransitionRange = 0.3f;

    /// <summary>
    /// At full return strictness, the alignment threshold shifts this high.
    /// 0.8 means only directions within ~37° of center escape penalty.
    /// </summary>
    private const float MaxReturnThreshold = 0.8f;

    /// <summary>
    /// Calculates the score adjustment for a single direction based on its alignment
    /// with the center direction. Directions pointing away from center get negative
    /// scores. Directions toward center are unaffected (zero).
    ///
    /// When normalizedDistance > 1.0 (outside zone), progressively shifts the penalty
    /// threshold so non-inward directions are also penalized — creating a rubber-band
    /// return force. No positive scores are added; inward directions are attractive
    /// by being the only ones without a penalty.
    /// </summary>
    public static float CalculateDirectionPenalty(
        Vector3 direction, Vector3 towardCenter, float penaltyStrength, float weight,
        float normalizedDistance = 0f)
    {
        if (penaltyStrength <= 0f)
        {
            return 0f;
        }

        float alignment = direction.Dot(towardCenter);

        // When outside zone, shift the "safe" threshold upward so non-inward
        // directions get penalized. The further outside, the stricter.
        float threshold = 0f;
        if (normalizedDistance > 1.0f)
        {
            float overshoot = Mathf.Clamp(
                (normalizedDistance - 1.0f) / ReturnTransitionRange, 0f, 1f);
            threshold = overshoot * MaxReturnThreshold;
        }

        float adjustedAlignment = alignment - threshold;

        if (adjustedAlignment >= 0f)
        {
            return 0f;
        }

        return adjustedAlignment * penaltyStrength * weight;
    }

    #endregion

    #region Test Helpers
#if TOOLS
    internal void SetBoundaryZoneKey(StringName key) => _boundaryZoneKey = key;
    internal void SetPenaltyMaxWeight(float value) => _penaltyMaxWeight = value;
    internal void SetPenaltyRampStart(float value) => _penaltyRampStart = value;
    internal void SetPenaltyRampEnd(float value) => _penaltyRampEnd = value;
    internal void SetPenaltyCurve(Curve? curve) => _penaltyCurve = curve;
    internal void SetZoneShape(ZoneShape3D? shape) => _zoneShape = shape;
    internal float GetAttractionMaxWeight() => _attractionMaxWeight;
    internal void SetAttractionMaxWeight(float value) => _attractionMaxWeight = value;
    internal void SetAttractionRampStart(float value) => _attractionRampStart = value;
    internal void SetAttractionRampEnd(float value) => _attractionRampEnd = value;
    internal void SetAttractionCurve(Curve? curve) => _attractionCurve = curve;
#endif
    #endregion
}
