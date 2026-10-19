namespace Jmodot.Implementation.AI.Navigation.Goals;

using System.Collections.Generic;
using Core.AI;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Jmodot.Implementation.AI.Navigation.Targets;
using Jmodot.Implementation.Shared;
using Godot;

/// <summary>
/// Holds a distance band and never ends itself: inside the band it pivots on the target, outside it
/// walks back. Unlike the approach goal, equal bounds are legal and make a point band. End the step
/// with a time limit or a later branch.
/// </summary>
[GlobalClass, Tool]
public partial class HoldRangeGoal3D : SteeredGoal3D
{
    /// <summary>
    /// The inclusive band to hold, in metres. Equal bounds are legal and mean "stand exactly this
    /// far away"; the goal never ends on arrival.
    /// </summary>
    [Export] public RangeBandResource? Band { get; private set; }

    public override bool EndsItself => false;

    public override GoalDistanceBand? DistanceBand
        => IsUsable(Band)
            ? new GoalDistanceBand(Band!.MinRange, Band.MaxRange, Target?.Geometry ?? TargetSampleGeometry.Member)
            : null;

    public override GoalStatus Advance(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        var sample = ResolveTarget(ctx, runtime);
        if (!IsUsable(Band)) { return GoalStatus.Failed; }
        if (sample.State != TargetSampleState.Resolved) { return GoalStatus.Failed; }
        return GoalStatus.Running;
    }

    protected override Dictionary<Vector3, float> ScoreSample(DirectionSet3D directions,
        TargetSample sample, SteeringDecisionContext3D ctx, AIConsiderationRuntime? runtime)
    {
        if (!IsUsable(Band)) { return ZeroScores(directions); }
        return AggregateBand(directions, sample, ctx.AgentPosition, runtime);
    }

    protected override RangeBandResource? ResponseBand => Band;

    public override string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>(base.GetResourceConfigurationWarnings());
        if (Band == null)
        {
            warnings.Add("Band is empty. Assign the inclusive distance band this goal holds.");
        }
        else if (!IsUsable(Band))
        {
            warnings.Add($"Band must be finite with 0 <= MinRange <= MaxRange; it is " +
                         $"{Band.MinRange}..{Band.MaxRange}. Equal bounds are legal.");
        }
        return warnings.ToArray();
    }

    /// <summary>The hold band's own rule: equal bounds are a point band, unlike an approach band.</summary>
    private static bool IsUsable(RangeBandResource? band)
        => band != null
           && float.IsFinite(band.MinRange) && float.IsFinite(band.MaxRange)
           && band.MinRange >= 0f && band.MinRange <= band.MaxRange;

    #region Test Helpers
#if TOOLS
    internal void SetBandForTest(RangeBandResource? band) => Band = band;
#endif
    #endregion
}
