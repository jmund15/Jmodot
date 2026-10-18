namespace Jmodot.Implementation.AI.Navigation.Goals;

using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Godot;
using Jmodot.Implementation.AI.Navigation.Considerations;
using Jmodot.Implementation.Shared;

/// <summary>
/// A targeted goal that also contributes a steering direction while it runs. Its Weight is the
/// only cross-term magnitude knob, and a suppression zone zeroes its pull without stopping it:
/// the goal keeps running and still owns its completion.
/// </summary>
[GlobalClass, Tool]
public abstract partial class SteeredGoal3D : TargetedMovementGoal3D
{
    private const float MaxWeight = 5f;

    /// <summary>
    /// How loudly this goal's direction competes with style terms, 0–5. Zero is legal: the goal
    /// stays registered and steers nothing, which the host warns about. A negative, non-finite or
    /// larger value is a fault: the goal warns, the action refuses to start and the goal scores zero.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 5.0, 0.05")]
    public float Weight { get; private set; } = 1f;

    /// <summary>
    /// Optional BB key holding a Vector4 zone (center xyz + radius in W). While its radius is
    /// positive this goal's pull is suppressed; it keeps running and still owns its completion.
    /// Empty means never suppressed.
    /// </summary>
    [Export] public StringName? SuppressWhileZoneKey { get; private set; }

    public sealed override bool Steers => true;

    public sealed override float SteeringWeight => Weight;

    private bool HasValidWeight => float.IsFinite(Weight) && Weight >= 0f && Weight <= MaxWeight;

    /// <summary>
    /// Scores from the sample the last Advance resolved. Reads no clock and never re-resolves, so
    /// repeated calls in one tick are pure.
    /// </summary>
    public sealed override Dictionary<Vector3, float> Score(DirectionSet3D directions,
        SteeringDecisionContext3D ctx, IBlackboard bb, AIConsiderationRuntime? runtime)
    {
        var targetRuntime = RuntimeOf(runtime);
        var sample = targetRuntime.Sample;
        if (!HasValidWeight || sample.State != Targets.TargetSampleState.Resolved)
        {
            return ZeroScores(directions);
        }
        if (Considerations.ZoneSuppression.IsSuppressed(bb, SuppressWhileZoneKey, this, targetRuntime))
        {
            return ZeroScores(directions);
        }
        return ScoreSample(directions, sample, ctx, runtime);
    }

    /// <summary>This goal's direction over the resolved sample; the goal's style, not its completion.</summary>
    protected abstract Dictionary<Vector3, float> ScoreSample(DirectionSet3D directions,
        Targets.TargetSample sample, SteeringDecisionContext3D ctx, AIConsiderationRuntime? runtime);

    /// <summary>The settings every goal scores with: confidence-weighted, no projection, flat.</summary>
    protected static TargetResponseSettings GoalSettings
        => new(confidenceWeighted: true, projectionInfluence: 0f, approachSpeedWeight: 0f, hasVerticalMovement: false);

    /// <summary>
    /// Aggregates a per-member response over the sample with the goal defaults, through the
    /// runtime's reusable buffer. Pass a callback cached once (a static field, or a field the goal
    /// fills on first use): a lambda written at the call site allocates every tick.
    /// </summary>
    protected Dictionary<Vector3, float> Aggregate(DirectionSet3D directions,
        Targets.TargetSample sample, Vector3 agentPosition,
        System.Func<Targets.TargetMember, Vector3, float, Vector3> response,
        AIConsiderationRuntime? runtime)
        => Considerations.TargetResponseScoring3D.Aggregate(
            directions, sample, agentPosition, GoalSettings, response, RuntimeOf(runtime).ResponseBuffer);

    /// <summary>The response of a goal that only opens distance: directly away from the member.</summary>
    protected static readonly System.Func<Targets.TargetMember, Vector3, float, Vector3> AwayResponse
        = (_, toMember, _) => Considerations.BandScoring.Away(toMember);

    protected static Dictionary<Vector3, float> ZeroScores(DirectionSet3D directions)
        => Considerations.DirectionScoring.ZeroScores(directions);

    public override string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>(base.GetResourceConfigurationWarnings());
        if (!HasValidWeight)
        {
            warnings.Add($"Weight must be a finite number from 0 to {MaxWeight} (it is {Weight}).");
        }
        return warnings.ToArray();
    }

    #region Test Helpers
#if TOOLS
    internal void SetWeightForTest(float weight) => Weight = weight;
    internal void SetSuppressWhileZoneKeyForTest(StringName? key) => SuppressWhileZoneKey = key;
#endif
    #endregion
}
