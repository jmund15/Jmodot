namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.AI.Perception;
using Core.Identification;
using Core.Movement;
using Jmodot.Core.Shared.Attributes;
using Shared;

/// <summary>
/// A unified steering consideration for reacting to dynamic targets (threats, prey, allies).
/// Replaces FleeConsideration3D, MultiFleeConsideration3D, PerceptionFleeConsideration3D,
/// and VelocityBody3DConsideration with a single configurable component.
///
/// Two continuous sliders create a 2D personality space:
///   _threatResolution: 0=overwhelmed/cornerable → 1=analytical/gap-finding
///   _threatFocus: 0=all threats equal → 1=tunnel vision on dominant threat
///
/// Two velocity dimensions:
///   _velocityInfluence: How much target velocity affects response DIRECTION (prediction)
///   _approachSpeedWeight: How much closing speed affects threat DANGER weight
///
/// All parameters are accessed via Effective*() methods for future BaseParameterDefinition support.
/// </summary>
[GlobalClass, Tool]
public partial class DynamicTargetConsideration3D : BaseAIConsideration3D
{
    #region Exports

    [ExportGroup("Behavior")]

    /// <summary>
    /// Positive values activate Chase mode, negative values activate Avoid mode.
    /// Magnitude determines the strength of the consideration.
    /// </summary>
    [Export(PropertyHint.Range, "-5.0, 5.0, 0.1")]
    private float _considerationWeight = -1.0f;

    /// <summary>
    /// Category filter for which perceived targets this consideration reacts to.
    /// </summary>
    [Export, RequiredExport] private Category _targetCategory = null!;

    [ExportGroup("Threat Processing")]

    /// <summary>
    /// Controls how multiple threats are aggregated into directional scores.
    /// 0.0 = Overwhelmed: sum all threat vectors first, then score. Produces cornered
    ///        emergence when opposing threats cancel to zero.
    /// 1.0 = Analytical: score each threat independently, then sum. Finds gaps between
    ///        threats. Never freezes.
    /// Intermediate values blend both strategies.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _threatResolution = 0.0f;

    /// <summary>
    /// Controls how attention is distributed across threats.
    /// 0.0 = All threats weighted equally.
    /// 1.0 = Tunnel vision: only the most dangerous threat matters, others ignored.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _threatFocus = 0.0f;

    [ExportGroup("Velocity")]

    /// <summary>
    /// How much target velocity affects the response DIRECTION.
    /// 0.0 = Pure position-based (flee from / chase toward current position).
    /// 1.0 = Fully predictive (flee from / chase toward projected future position).
    /// Uses target velocity (not relative velocity) — answers "where is the threat GOING?"
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _velocityInfluence = 0.0f;

    /// <summary>
    /// How much closing speed affects per-threat DANGER weight.
    /// 0.0 = Threat weight unaffected by approach velocity.
    /// 1.0 = Threats approaching at high speed are weighted significantly more.
    /// Uses relative velocity dot product — answers "how fast is it CLOSING?"
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _approachSpeedWeight = 0.0f;

    [ExportGroup("Options")]

    /// <summary>Scale each threat's contribution by its perception confidence.</summary>
    [Export] private bool _confidenceWeighted = true;

    /// <summary>Include Y axis in calculations. Disable for ground-based agents.</summary>
    [Export] private bool _hasVerticalMovement = false;

    #endregion

    private const float Epsilon = 0.001f;
    private bool _missingMemoryLogged;
    private readonly List<TargetData> _targetDataCache = new();

    /// <summary>Per-target computed data for the two-pass algorithm.</summary>
    private readonly struct TargetData
    {
        public readonly Vector3 ResponseDirection;
        public readonly float Weight;

        public TargetData(Vector3 responseDirection, float weight)
        {
            ResponseDirection = responseDirection;
            Weight = weight;
        }
    }

    #region Effective Parameter Accessors

    // Current: return exported value directly.
    // Future: resolve via BaseParameterDefinition for emotion/stat-driven parameters.
    private float EffectiveThreatResolution(IBlackboard _) => _threatResolution;
    private float EffectiveThreatFocus(IBlackboard _) => _threatFocus;
    private float EffectiveVelocityInfluence(IBlackboard _) => _velocityInfluence;
    private float EffectiveApproachSpeedWeight(IBlackboard _) => _approachSpeedWeight;

    #endregion

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard)
    {
        var zeroScores = directions.Directions.ToDictionary(dir => dir, _ => 0f);

        if (_targetCategory == null) { return zeroScores; }

        if (context3D.Memory == null)
        {
            if (!_missingMemoryLogged)
            {
                JmoLogger.Error(this,
                    $"DynamicTargetConsideration3D '{ResourceName}' requires an AIPerceptionManager3D " +
                    "but the steering context has no Memory. This consideration will have no effect.");
                _missingMemoryLogged = true;
            }
            return zeroScores;
        }

        var targets = context3D.Memory.GetSensedByCategory(_targetCategory);

        float effectiveVelocity = EffectiveVelocityInfluence(blackboard);
        float effectiveApproach = EffectiveApproachSpeedWeight(blackboard);
        float effectiveFocus = EffectiveThreatFocus(blackboard);
        float effectiveResolution = EffectiveThreatResolution(blackboard);
        bool isChase = _considerationWeight > 0;
        float weightMagnitude = Mathf.Abs(_considerationWeight);

        // Pass 1: Compute per-target response direction and raw weight
        _targetDataCache.Clear();
        float maxWeight = 0f;

        foreach (var target in targets)
        {
            Vector3 effectivePos = ComputeEffectivePosition(target, effectiveVelocity);
            Vector3 toTarget = effectivePos - context3D.AgentPosition;
            if (!_hasVerticalMovement) { toTarget.Y = 0; }
            if (toTarget.LengthSquared() < Epsilon * Epsilon) { continue; }

            Vector3 toTargetDir = toTarget.Normalized();
            Vector3 responseDir = isChase ? toTargetDir : -toTargetDir;

            float weight = ComputeThreatWeight(target, toTargetDir, effectiveApproach);
            if (weight < Epsilon) { continue; }

            maxWeight = Mathf.Max(maxWeight, weight);
            _targetDataCache.Add(new TargetData(responseDir, weight));
        }

        if (_targetDataCache.Count == 0) { return zeroScores; }

        // Pass 2: Aggregate using _threatResolution blend
        return AggregateScores(directions, effectiveFocus, effectiveResolution,
            weightMagnitude, maxWeight);
    }

    #region Core Computation

    /// <summary>
    /// Gets the effective position for a target, using velocity projection scaled by influence.
    /// At 0: raw LastKnownPosition. At 1: full velocity-projected position.
    /// </summary>
    private static Vector3 ComputeEffectivePosition(Perception3DInfo target, float velocityInfluence)
        => target.GetProjectedPosition(velocityInfluence);

    /// <summary>
    /// Computes the weight (danger level) for a single target.
    /// Factors: confidence (memory freshness) and approach speed (closing velocity).
    /// </summary>
    private float ComputeThreatWeight(Perception3DInfo target, Vector3 toTargetDir,
        float effectiveApproach)
    {
        float weight = 1f;

        if (_confidenceWeighted)
        {
            weight *= target.CurrentConfidence;
        }

        if (effectiveApproach > Epsilon)
        {
            Vector3 toAgentDir = -toTargetDir;
            float closingSpeed = Mathf.Max(0f, target.LastKnownVelocity.Dot(toAgentDir));
            // Scale: 5 m/s closing at full weight ≈ 2x multiplier
            weight *= (1f + closingSpeed * 0.2f * effectiveApproach);
        }

        return weight;
    }

    /// <summary>
    /// Applies threat focus weighting: at focus=0, all threats equal; at focus=1, only dominant.
    /// </summary>
    private static float ApplyFocusWeight(float weight, float maxWeight, float focus)
    {
        if (focus < Epsilon) { return weight; }
        bool isDominant = Mathf.IsEqualApprox(weight, maxWeight);
        return isDominant ? weight : weight * (1f - focus);
    }

    /// <summary>
    /// Blends CombineFirst and PerTarget aggregation strategies based on threat resolution.
    /// </summary>
    private Dictionary<Vector3, float> AggregateScores(DirectionSet3D directions,
        float effectiveFocus, float effectiveResolution, float weightMagnitude, float maxWeight)
    {
        // CombineFirst: sum all weighted response vectors, then score directions
        var combinedDirection = Vector3.Zero;
        foreach (var td in _targetDataCache)
        {
            float focusedWeight = ApplyFocusWeight(td.Weight, maxWeight, effectiveFocus);
            combinedDirection += td.ResponseDirection * focusedWeight;
        }
        var combineFirstScores = ScoreByAlignment(directions, combinedDirection, weightMagnitude);

        // Optimization: skip PerTarget if resolution is 0
        if (effectiveResolution < Epsilon) { return combineFirstScores; }

        // PerTarget: score each target independently, then sum
        var perTargetScores = directions.Directions.ToDictionary(d => d, _ => 0f);
        foreach (var td in _targetDataCache)
        {
            float focusedWeight = ApplyFocusWeight(td.Weight, maxWeight, effectiveFocus);
            var targetScores = ScoreByAlignment(directions, td.ResponseDirection,
                weightMagnitude * focusedWeight);
            foreach (var dir in directions.Directions)
            {
                perTargetScores[dir] += targetScores[dir];
            }
        }

        // Optimization: skip blend if resolution is 1
        if (effectiveResolution > 1f - Epsilon) { return perTargetScores; }

        // Blend CombineFirst and PerTarget
        var blended = new Dictionary<Vector3, float>();
        foreach (var dir in directions.Directions)
        {
            float cf = combineFirstScores.GetValueOrDefault(dir, 0f);
            float pt = perTargetScores.GetValueOrDefault(dir, 0f);
            blended[dir] = Mathf.Lerp(cf, pt, effectiveResolution);
        }
        return blended;
    }

    /// <summary>
    /// Scores each direction by alignment with a target direction vector.
    /// Preserves the magnitude of the target direction in the score (encodes urgency).
    /// </summary>
    private Dictionary<Vector3, float> ScoreByAlignment(DirectionSet3D directions,
        Vector3 targetDirection, float scaleFactor)
    {
        var scores = directions.Directions.ToDictionary(d => d, _ => 0f);

        float magnitude = targetDirection.Length();
        if (magnitude < Epsilon) { return scores; }

        Vector3 normalizedDir = targetDirection / magnitude;
        float combinedScale = magnitude * scaleFactor;

        foreach (var dir in directions.Directions)
        {
            Vector3 flatDir = dir;
            if (!_hasVerticalMovement)
            {
                flatDir.Y = 0;
                if (flatDir.LengthSquared() < Epsilon) { continue; }
                flatDir = flatDir.Normalized();
            }

            float alignment = flatDir.Dot(normalizedDir);
            if (alignment > 0f)
            {
                scores[dir] = alignment * combinedScale;
            }
        }

        return scores;
    }

    #endregion

    #region Test Helpers
#if TOOLS
    internal void SetTargetCategory(Category category) => _targetCategory = category;
    internal void SetConsiderationWeight(float value) => _considerationWeight = value;
    internal void SetThreatResolution(float value) => _threatResolution = value;
    internal void SetThreatFocus(float value) => _threatFocus = value;
    internal void SetVelocityInfluence(float value) => _velocityInfluence = value;
    internal void SetApproachSpeedWeight(float value) => _approachSpeedWeight = value;
    internal void SetConfidenceWeighted(bool value) => _confidenceWeighted = value;
#endif
    #endregion
}
