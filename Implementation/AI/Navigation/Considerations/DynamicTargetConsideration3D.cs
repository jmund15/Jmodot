namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.AI.Perception;
using Core.Identification;
using Core.Movement;
using Core.Shared;
using Jmodot.Implementation.AI.Navigation.Targets;
using Shared;
using Shared.GodotExceptions;

/// <summary>How a consideration reacts to a perceived target: steer toward it, or away from it.</summary>
public enum TargetResponseMode
{
    Chase,
    Avoid,
}

/// <summary>
/// A unified steering consideration for reacting to dynamic targets (threats, prey, allies).
///
/// Two continuous sliders create a 2D personality space:
///   <see cref="_threatResolution"/>: 0=overwhelmed/cornerable → 1=analytical/gap-finding
///   <see cref="_threatFocus"/>: 0=all threats equal → 1=tunnel vision on dominant threat
///
/// Two velocity dimensions:
///   <see cref="_velocityInfluence"/>: How much target velocity affects response DIRECTION (prediction)
///   <see cref="_approachSpeedWeight"/>: How much closing speed affects threat DANGER weight
///
/// The combination math lives in <see cref="TargetResponseScoring3D"/> and the alignment loop in
/// <see cref="DirectionScoring"/>, so goals score through the same owners.
/// </summary>
[GlobalClass, Tool]
public partial class DynamicTargetConsideration3D : BaseAIConsideration3D, IResourceConfigurationWarnings
{
    #region Exports

    /// <summary>
    /// Chase steers toward perceived targets; Avoid steers away (directions mirrored).
    /// Contribution magnitude is owned by the base Weight, not by this mode.
    /// </summary>
    [ExportGroup("Behavior")]
    [Export] public TargetResponseMode Mode { get; private set; } = TargetResponseMode.Chase;

    /// <summary>
    /// Category filter for perceived targets. A non-empty <see cref="_trackedTargetKey"/> wins
    /// and hides this field. With neither assigned, tracks the primary-target memory.
    /// </summary>
    [Export] private Category? _targetCategory;

    /// <summary>
    /// Optional BB key holding the <see cref="Perception3DInfo"/> of one target. A non-empty key
    /// overrides <see cref="_targetCategory"/>. Empty uses the category if assigned, otherwise
    /// <see cref="Jmodot.Implementation.AI.BB.BBDataSig.PrimaryTargetMemory"/>.
    /// </summary>
    [Export] private StringName? _trackedTargetKey;

    /// <summary>
    /// Optional BB key holding a Vector4 zone (center xyz + radius in W). While that zone is active
    /// (radius W &gt; 0) this consideration is fully suppressed (contributes zero scores), letting a
    /// project spatially gate a chase/avoid without the framework knowing the gate's meaning. Unset
    /// (default) = never suppressed. Mirrors <c>ZoneBoundaryConsideration3D._boundaryZoneKey</c>'s
    /// Vector4-BB convention.
    /// </summary>
    [Export] private StringName? _suppressWhileZoneKey;

    /// <summary>
    /// Controls how multiple threats are aggregated into directional scores.
    /// 0.0 = Overwhelmed: sum all threat vectors first, then score. Produces cornered
    ///        emergence when opposing threats cancel to zero.
    /// 1.0 = Analytical: score each threat independently, then sum. Finds gaps between
    ///        threats. Never freezes.
    /// Intermediate values blend both strategies.
    /// </summary>
    [ExportGroup("Threat Processing")]
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _threatResolution = 0.0f;

    /// <summary>
    /// Controls how attention is distributed across threats.
    /// 0.0 = All threats weighted equally.
    /// 1.0 = Tunnel vision: only the most dangerous threat matters, others ignored.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _threatFocus = 0.0f;

    /// <summary>
    /// How much target velocity affects the response DIRECTION.
    /// 0.0 = Pure position-based (flee from / chase toward current position).
    /// 1.0 = Fully predictive (flee from / chase toward projected future position).
    /// Uses target velocity (not relative velocity) — answers "where is the threat GOING?"
    /// </summary>
    [ExportGroup("Velocity")]
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

    /// <summary>Scale each threat's contribution by its perception confidence.</summary>
    [ExportGroup("Options")]
    [Export] private bool _confidenceWeighted = true;

    /// <summary>Include Y axis in calculations. Disable for ground-based agents.</summary>
    [Export] private bool _hasVerticalMovement = false;

    #endregion

    private const float Epsilon = 0.001f;

    /// <summary>
    /// Per-agent state: the resolved sample, the response scratch buffer and the latches that keep
    /// a diagnostic to one line per agent. The Resource itself stays shared.
    /// </summary>
    private sealed class DynamicRuntime : TargetRuntime
    {
        public bool MissingMemoryLogged;
        public bool TrackedKeyChecked;
    }

    public override AIConsiderationRuntime? CreateRuntime(IBlackboard? blackboard) => new DynamicRuntime();

    private static readonly Func<TargetMember, Vector3, float, Vector3> ChaseResponse =
        (_, toTarget, _) => toTarget.Normalized();

    private static readonly Func<TargetMember, Vector3, float, Vector3> AvoidResponse =
        (_, toTarget, _) => -toTarget.Normalized();

    private bool HasExplicitTrackedKey => !string.IsNullOrEmpty(_trackedTargetKey);
    private bool IsTracking => HasExplicitTrackedKey || _targetCategory == null;
    private StringName ResolvedTrackedKey => HasExplicitTrackedKey
        ? _trackedTargetKey! : Jmodot.Implementation.AI.BB.BBDataSig.PrimaryTargetMemory;

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        var name = property["name"].AsString();
        var usage = (PropertyUsageFlags)property["usage"].AsInt64();
        if (name == nameof(_trackedTargetKey))
        {
            property["usage"] = (long)(usage | PropertyUsageFlags.UpdateAllIfModified);
        }
        else if (name == nameof(_targetCategory) && HasExplicitTrackedKey)
        {
            property["usage"] = (long)(usage & ~PropertyUsageFlags.Editor);
        }
    }

    public string[] GetResourceConfigurationWarnings()
    {
        if (_targetCategory != null && HasExplicitTrackedKey)
        {
            return new[] { "Target Category and Tracked Target Key are both set. The key wins and the category is ignored; clear Target Category." };
        }
        return System.Array.Empty<string>();
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
        DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard,
        AIConsiderationRuntime? runtime)
    {
        var state = NarrowRuntime<DynamicRuntime>(runtime);

        if (IsSuppressedByZone(blackboard)) { return ZeroScores(directions); }

        bool tracked = IsTracking;
        if (!tracked && _targetCategory == null) { return ZeroScores(directions); }

        if (!tracked && context3D.Memory == null)
        {
            if (state == null || !state.MissingMemoryLogged)
            {
                JmoLogger.Error(this,
                    $"DynamicTargetConsideration3D '{ResourceName}' requires an AIPerceptionManager3D " +
                    "but the steering context has no Memory. This consideration will have no effect.");
                if (state != null) { state.MissingMemoryLogged = true; }
            }
            return ZeroScores(directions);
        }

        if (tracked && (state == null || !state.TrackedKeyChecked))
        {
            if (state != null) { state.TrackedKeyChecked = true; }
            WarnWhenNothingUnderTrackedKey(blackboard);
        }

        var sample = state?.Sample ?? new TargetSample();
        sample.Clear();
        sample.Resolution = EffectiveThreatResolution(blackboard);
        sample.Focus = EffectiveThreatFocus(blackboard);

        if (!tracked)
        {
            var members = new List<TargetMember>();
            foreach (var target in context3D.Memory!.GetSensedByCategory(_targetCategory!))
            {
                members.Add(TargetMember.FromMemory(target, context3D.AgentPosition));
            }
            sample.Adopt(members, TargetSampleGeometry.Set, sample.Resolution, sample.Focus);
        }
        else if (blackboard.TryGetLiveTarget(ResolvedTrackedKey, out var trackedTarget, out _))
        {
            sample.SetSingle(TargetMember.FromMemory(trackedTarget, context3D.AgentPosition));
            sample.Resolution = EffectiveThreatResolution(blackboard);
            sample.Focus = EffectiveThreatFocus(blackboard);
        }

        var settings = new TargetResponseSettings(
            confidenceWeighted: _confidenceWeighted,
            projectionInfluence: EffectiveVelocityInfluence(blackboard),
            approachSpeedWeight: EffectiveApproachSpeedWeight(blackboard),
            hasVerticalMovement: _hasVerticalMovement);

        var scratch = state?.ResponseBuffer ?? new List<TargetResponse>();
        return TargetResponseScoring3D.Aggregate(directions, sample, context3D.AgentPosition, settings,
            Mode == TargetResponseMode.Chase ? ChaseResponse : AvoidResponse, scratch);
    }

    #region Core Computation

    /// <summary>Logs once per runtime when the blackboard holds no memory under the tracked key; an inactive memory is not missing.</summary>
    private void WarnWhenNothingUnderTrackedKey(IBlackboard blackboard)
    {
        if (blackboard.TryGet<Perception3DInfo>(ResolvedTrackedKey, out var tracked) && tracked != null) { return; }

        JmoLogger.Warning(this,
            $"DynamicTargetConsideration3D '{ResourceName}' reads tracked target key '{ResolvedTrackedKey}' but the blackboard " +
            "holds no Perception3DInfo under it. Check the key against the one the target tracker publishes; " +
            "this consideration scores zero until a memory appears.");
    }

    /// <summary>
    /// True when a suppression zone key is configured and its BB Vector4 has a positive radius (W).
    /// </summary>
    private bool IsSuppressedByZone(IBlackboard blackboard)
        => !string.IsNullOrEmpty(_suppressWhileZoneKey)
           && blackboard.TryGet<Vector4>(_suppressWhileZoneKey!, out var zone)
           && zone.W > 0f;

    private static Dictionary<Vector3, float> ZeroScores(DirectionSet3D directions)
        => directions.Directions.ToDictionary(dir => dir, _ => 0f);

    #endregion

    #region Test Helpers
#if TOOLS
    internal void SetTargetCategory(Category? category) => _targetCategory = category!;
    internal void SetSuppressWhileZoneKey(StringName key) => _suppressWhileZoneKey = key;
    internal void SetTrackedTargetKey(StringName key) => _trackedTargetKey = key;
    internal void SetMode(TargetResponseMode mode) => Mode = mode;
    internal void SetThreatResolution(float value) => _threatResolution = value;
    internal void SetThreatFocus(float value) => _threatFocus = value;
    internal void SetVelocityInfluence(float value) => _velocityInfluence = value;
    internal void SetApproachSpeedWeight(float value) => _approachSpeedWeight = value;
    internal void SetConfidenceWeighted(bool value) => _confidenceWeighted = value;
    internal void SetHasVerticalMovement(bool value) => _hasVerticalMovement = value;
#endif
    #endregion
}
