namespace Jmodot.Implementation.AI.Navigation.Goals;

using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Jmodot.Implementation.AI.Navigation.Targets;
using Godot;

/// <summary>
/// Keeps moving away from every member and never ends itself; an empty target leaves it running,
/// so it keeps fleeing through a moment of lost sight. Accepts a whole set. It has no distance or
/// timer of its own: end the step with a time limit or a later branch.
/// </summary>
[GlobalClass, Tool]
public partial class FleeGoal3D : SteeredGoal3D
{
    public override bool EndsItself => false;

    protected override bool AcceptsSetTarget => true;

    public override GoalStatus Advance(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        var sample = ResolveTarget(ctx, runtime);
        return sample.State == TargetSampleState.Unavailable ? GoalStatus.Failed : GoalStatus.Running;
    }

    protected override Dictionary<Vector3, float> ScoreSample(DirectionSet3D directions,
        TargetSample sample, SteeringDecisionContext3D ctx, AIConsiderationRuntime? runtime)
        => Aggregate(directions, sample, ctx.AgentPosition,
            (_, toMember, _) => Considerations.BandScoring.Away(toMember),
            new List<Considerations.TargetResponse>());
}
