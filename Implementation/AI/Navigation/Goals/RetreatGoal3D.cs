namespace Jmodot.Implementation.AI.Navigation.Goals;

using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Jmodot.Implementation.AI.Navigation.Targets;
using Jmodot.Implementation.Shared;
using Godot;

/// <summary>
/// Opens the distance until every member is at or past the safe distance, then succeeds. Accepts a
/// whole set. Use FleeGoal3D instead when the escape should continue rather than finish.
/// </summary>
[GlobalClass, Tool]
public partial class RetreatGoal3D : SteeredGoal3D
{
    /// <summary>
    /// The distance, in metres, at which every member of the target is considered escaped. The goal
    /// finishes on it. Continuous escape is FleeGoal3D.
    /// </summary>
    [Export(PropertyHint.Range, "0.5,50.0,0.5,suffix:m")]
    public float SafeDistance { get; private set; } = 6f;

    protected override bool AcceptsSetTarget => true;

    public override GoalDistanceBand? DistanceBand
        => IsSafeDistanceUsable
            ? new GoalDistanceBand(SafeDistance, float.PositiveInfinity,
                Target?.Geometry ?? TargetSampleGeometry.Member)
            : null;

    public override GoalStatus Advance(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        var sample = ResolveTarget(ctx, runtime);

        if (!IsSafeDistanceUsable)
        {
            FailOnce(sample, $"{SafeDistance}");
            return GoalStatus.Failed;
        }

        if (sample.State == TargetSampleState.Unavailable) { return GoalStatus.Failed; }
        if (sample.State == TargetSampleState.Empty) { return GoalStatus.Succeeded; }

        foreach (var member in sample.Members)
        {
            if (member.FlatDistance < SafeDistance) { return GoalStatus.Running; }
        }
        return GoalStatus.Succeeded;
    }

    protected override Dictionary<Vector3, float> ScoreSample(DirectionSet3D directions,
        TargetSample sample, SteeringDecisionContext3D ctx, AIConsiderationRuntime? runtime)
        => !IsSafeDistanceUsable
            ? ZeroScores(directions)
            : Aggregate(directions, sample, ctx.AgentPosition,
                (_, toMember, _) => Considerations.BandScoring.Away(toMember),
                new List<Considerations.TargetResponse>());

    public override string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>(base.GetResourceConfigurationWarnings());
        if (!IsSafeDistanceUsable)
        {
            warnings.Add($"SafeDistance must be a finite distance greater than 0 (it is {SafeDistance}).");
        }
        return warnings.ToArray();
    }

    private bool IsSafeDistanceUsable => float.IsFinite(SafeDistance) && SafeDistance > 0f;

    private void FailOnce(TargetSample sample, string value)
    {
        if (!sample.TryBeginLog(TargetLogKind.ConfigError)) { return; }
        JmoLogger.Error(this,
            $"RetreatGoal3D '{ResourceName}' has SafeDistance {value}; it must be finite and greater " +
            "than 0, so the goal fails instead of retreating to nowhere.");
    }

    #region Test Helpers
#if TOOLS
    internal void SetSafeDistanceForTest(float value) => SafeDistance = value;
#endif
    #endregion
}
