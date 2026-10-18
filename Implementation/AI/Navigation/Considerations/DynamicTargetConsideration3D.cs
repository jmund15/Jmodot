namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
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
///   Resolution: 0=overwhelmed/cornerable → 1=analytical/gap-finding
///   Focus: 0=all threats equal → 1=tunnel vision on dominant threat
///
/// Two velocity dimensions:
///   <see cref="_velocityInfluence"/>: How much target velocity affects response DIRECTION (prediction)
///   <see cref="_approachSpeedWeight"/>: How much closing speed affects threat DANGER weight
///
/// Who the consideration reacts to is a <see cref="MovementTarget3D"/>; the combination math
/// lives in <see cref="TargetResponseScoring3D"/>, so goals score through the same owners.
/// </summary>
[GlobalClass, Tool]
public partial class DynamicTargetConsideration3D : BaseAIConsideration3D, IResourceConfigurationWarnings, IAgentConfigurationWarnings
{
    #region Exports

    /// <summary>
    /// What this consideration reacts to. Empty follows the primary target with no key to type;
    /// assign a TrackedTarget3D to follow another key, or a CategoryTarget3D to react to a group.
    /// A category target's Resolution and Focus are set on its Policy.
    /// </summary>
    [ExportGroup("Behavior")]
    [Export] public MovementTarget3D? Target { get; private set; }

    /// <summary>
    /// Chase steers toward perceived targets; Avoid steers away (directions mirrored).
    /// Contribution magnitude is owned by the base Weight, not by this mode.
    /// </summary>
    [Export] public TargetResponseMode Mode { get; private set; } = TargetResponseMode.Chase;

    /// <summary>
    /// Optional BB key holding a Vector4 zone (center xyz + radius in W). While that zone is active
    /// (radius W &gt; 0) this consideration is fully suppressed (contributes zero scores), letting a
    /// project spatially gate a chase/avoid without the framework knowing the gate's meaning. Unset
    /// (default) = never suppressed. Mirrors <c>ZoneBoundaryConsideration3D._boundaryZoneKey</c>'s
    /// Vector4-BB convention.
    /// </summary>
    [Export] private StringName? _suppressWhileZoneKey;

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

    public override AIConsiderationRuntime? CreateRuntime(IBlackboard? blackboard) => new TargetRuntime();

    private static readonly Func<TargetMember, Vector3, float, Vector3> ChaseResponse =
        (_, toTarget, _) => toTarget.Normalized();

    private static readonly Func<TargetMember, Vector3, float, Vector3> AvoidResponse =
        (_, toTarget, _) => -toTarget.Normalized();

    public string[] GetResourceConfigurationWarnings()
        => ConfigWarnings.ResourceSlotWarnings("Target", Target, required: false);

    /// <summary>The entity faults of the target this term reacts to, such as a category with no perception manager.</summary>
    public string[] GetAgentWarnings(Node agent) => Target?.GetAgentWarnings(agent) ?? Array.Empty<string>();

    #region Effective Parameter Accessors

    // Current: return exported value directly.
    // Future: resolve via BaseParameterDefinition for emotion/stat-driven parameters.
    private float EffectiveVelocityInfluence(IBlackboard _) => _velocityInfluence;
    private float EffectiveApproachSpeedWeight(IBlackboard _) => _approachSpeedWeight;

    #endregion

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard,
        AIConsiderationRuntime? runtime)
    {
        var state = NarrowRuntime<TargetRuntime>(runtime);

        if (ZoneSuppression.IsSuppressed(blackboard, _suppressWhileZoneKey, this, state))
        {
            return DirectionScoring.ZeroScores(directions);
        }

        var sample = state?.Sample ?? new TargetSample();
        MovementTarget3D.ResolveOrPrimary(
            Target, context3D.AgentPosition, blackboard, context3D.Memory, sample);
        if (sample.State != TargetSampleState.Resolved) { return DirectionScoring.ZeroScores(directions); }

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

    #endregion

    #region Test Helpers
#if TOOLS
    internal void SetTarget(MovementTarget3D? target) => Target = target;
    internal void SetSuppressWhileZoneKey(StringName key) => _suppressWhileZoneKey = key;
    internal void SetMode(TargetResponseMode mode) => Mode = mode;
    internal void SetVelocityInfluence(float value) => _velocityInfluence = value;
    internal void SetApproachSpeedWeight(float value) => _approachSpeedWeight = value;
    internal void SetConfidenceWeighted(bool value) => _confidenceWeighted = value;
    internal void SetHasVerticalMovement(bool value) => _hasVerticalMovement = value;
#endif
    #endregion
}
