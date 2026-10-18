namespace Jmodot.Implementation.AI.Navigation.Goals;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Godot;
using Jmodot.Implementation.AI.Navigation.Considerations;

/// <summary>
/// A targeted goal that also contributes a steering direction while it runs. Its Weight is the
/// only cross-term magnitude knob, and a suppression zone zeroes its pull without stopping it:
/// the goal keeps running and still owns its completion.
/// </summary>
[GlobalClass, Tool]
public abstract partial class SteeredGoal3D : TargetedMovementGoal3D
{
    /// <summary>
    /// How loudly this goal's direction competes with style terms, 0–5. Zero is legal: the goal
    /// stays registered and steers nothing, which the host warns about.
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

    /// <summary>
    /// Scores from the sample the last Advance resolved. Reads no clock and never re-resolves, so
    /// repeated calls in one tick are pure.
    /// </summary>
    public sealed override Dictionary<Vector3, float> Score(DirectionSet3D directions,
        SteeringDecisionContext3D ctx, IBlackboard bb, AIConsiderationRuntime? runtime)
    {
        var sample = (runtime as Targets.TargetRuntime)?.Sample;
        if (sample == null || sample.State != Targets.TargetSampleState.Resolved)
        {
            return ZeroScores(directions);
        }
        if (IsSuppressedByZone(bb)) { return ZeroScores(directions); }
        return ScoreSample(directions, sample, ctx);
    }

    /// <summary>This goal's direction over the resolved sample; the goal's style, not its completion.</summary>
    protected abstract Dictionary<Vector3, float> ScoreSample(DirectionSet3D directions,
        Targets.TargetSample sample, SteeringDecisionContext3D ctx);

    /// <summary>The settings every goal scores with: confidence-weighted, no projection, flat.</summary>
    protected static TargetResponseSettings GoalSettings
        => new(confidenceWeighted: true, projectionInfluence: 0f, approachSpeedWeight: 0f, hasVerticalMovement: false);

    /// <summary>Aggregates a per-member response over the sample with the goal defaults.</summary>
    protected static Dictionary<Vector3, float> Aggregate(DirectionSet3D directions,
        Targets.TargetSample sample, Vector3 agentPosition,
        System.Func<Targets.TargetMember, Vector3, float, Vector3> response,
        List<Considerations.TargetResponse> scratch)
        => Considerations.TargetResponseScoring3D.Aggregate(
            directions, sample, agentPosition, GoalSettings, response, scratch);

    protected static bool IsSuppressedByZone(IBlackboard bb, StringName? key)
        => !string.IsNullOrEmpty(key)
           && bb.TryGet<Vector4>(key!, out var zone)
           && zone.W > 0f;

    protected static Dictionary<Vector3, float> ZeroScores(DirectionSet3D directions)
        => directions.Directions.ToDictionary(dir => dir, _ => 0f);

    private bool IsSuppressedByZone(IBlackboard bb) => IsSuppressedByZone(bb, SuppressWhileZoneKey);

    #region Test Helpers
#if TOOLS
    internal void SetWeightForTest(float weight) => Weight = weight;
    internal void SetSuppressWhileZoneKeyForTest(StringName? key) => SuppressWhileZoneKey = key;
#endif
    #endregion
}
