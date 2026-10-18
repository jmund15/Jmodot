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
/// Moves straight at the target until it is inside the band, then succeeds. Direct steering: it
/// ignores walls and never requests a navigation route, so a goal that must path around geometry
/// belongs to the navigation family. Share an attack band when the goal exists to reach attack
/// range.
/// </summary>
[GlobalClass, Tool]
public partial class ApproachGoal3D : SteeredGoal3D
{
    /// <summary>
    /// The inclusive band this goal ends inside, in metres. Assign the shared band the attack or
    /// interaction it approaches also uses.
    /// </summary>
    [Export] public RangeBandResource? Band { get; private set; }

    public override GoalDistanceBand? DistanceBand
        => RangeBandResource.IsValid(Band)
            ? new GoalDistanceBand(Band!.MinRange, Band.MaxRange, Target?.Geometry ?? TargetSampleGeometry.Member)
            : null;

    public override GoalStatus Advance(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        var sample = ResolveTarget(ctx, runtime);
        if (!RangeBandResource.IsValid(Band)) { return GoalStatus.Failed; }
        if (sample.State != TargetSampleState.Resolved) { return GoalStatus.Failed; }
        return RangeBandResource.HasArrived(sample.NearestDistance, Band)
            ? GoalStatus.Succeeded
            : GoalStatus.Running;
    }

    protected override Dictionary<Vector3, float> ScoreSample(DirectionSet3D directions,
        TargetSample sample, SteeringDecisionContext3D ctx, AIConsiderationRuntime? runtime)
    {
        if (!RangeBandResource.IsValid(Band)) { return ZeroScores(directions); }
        float min = Band!.MinRange;
        float max = Band.MaxRange;
        return Aggregate(directions, sample, ctx.AgentPosition,
            (_, toMember, distance) => Considerations.BandScoring.Response(toMember, distance, min, max),
            new List<Considerations.TargetResponse>());
    }

    public override string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>(base.GetResourceConfigurationWarnings());
        if (Band == null)
        {
            warnings.Add("Band is empty. Assign the inclusive distance band this goal ends inside.");
        }
        else if (!RangeBandResource.IsValid(Band))
        {
            warnings.Add($"Band is unusable: MinRange ({Band.MinRange}) must be non-negative and " +
                         $"strictly less than MaxRange ({Band.MaxRange}).");
        }
        return warnings.ToArray();
    }

    #region Test Helpers
#if TOOLS
    internal void SetBandForTest(RangeBandResource? band) => Band = band;
#endif
    #endregion
}
