namespace Jmodot.Implementation.AI.Navigation.Goals;

using System.Collections.Generic;
using Core.AI;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Godot;
using Implementation.AI.BB;
using Implementation.Shared;
using Jmodot.AI.Navigation;
using Jmodot.Implementation.AI.Navigation.Targets;

/// <summary>
/// Walks a navmesh route to the target's last known position and succeeds inside the arrive band.
/// Hosted by a MoveAction, which claims navigation-only control for the step so the route is
/// followed through doors and around walls; fails when the target is gone. For a straight line
/// with no navmesh, use <see cref="ApproachGoal3D"/>.
/// </summary>
[GlobalClass, Tool]
public partial class NavApproachGoal3D : TargetedMovementGoal3D
{
    /// <summary>
    /// Distance from the target at which pursuit succeeds. Empty: follow the last known position
    /// until the memory fades. Walks a navmesh route; use ApproachGoal3D for a straight line.
    /// </summary>
    [Export] public RangeBandResource? ArriveBand { get; private set; }

    public override SteeringControlMode ControlMode => SteeringControlMode.NavigationOnly;

    public override GoalDistanceBand? DistanceBand
        => RangeBandResource.IsValid(ArriveBand)
            ? new GoalDistanceBand(ArriveBand!.MinRange, ArriveBand.MaxRange,
                Target?.Geometry ?? TargetSampleGeometry.Member)
            : null;

    public override AIConsiderationRuntime? CreateRuntime(IBlackboard? bb) => new NavApproachRuntime();

    public override void OnEnter(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        if (runtime is not NavApproachRuntime rt) { return; }

        ctx.BB.TryGet<AINavigator3D>(BBDataSig.AINavComp, out var nav);
        rt.Navigator = nav;
        rt.PendingFirstRequest = true;
    }

    public override GoalStatus Advance(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        var sample = ResolveTarget(ctx, runtime);
        if (sample.State != TargetSampleState.Resolved) { return GoalStatus.Failed; }

        if (runtime is not NavApproachRuntime { Navigator: { } nav } rt)
        {
            if (runtime is NavApproachRuntime { WarnedNoNavigator: false } unwarned)
            {
                unwarned.WarnedNoNavigator = true;
                JmoLogger.Warning(this, $"Goal '{ResourceName}': no AINavigator3D on the blackboard, cannot pursue.");
            }
            return GoalStatus.Failed;
        }

        if (RangeBandResource.HasArrived(sample.NearestDistance, ArriveBand)) { return GoalStatus.Succeeded; }

        if (rt.PendingFirstRequest)
        {
            if (!nav.IsMapReady()) { return GoalStatus.Running; }
            rt.PendingFirstRequest = false;
        }

        nav.RequestNewNavPath(sample.Members[0].Position);
        return GoalStatus.Running;
    }

    public override void OnExit(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        if (runtime is not NavApproachRuntime { Navigator: { } nav }) { return; }

        try { nav.ClearPath(); }
        catch (System.NullReferenceException)
        {
            // A navigator whose _Ready never ran has no owner agent to clear; safe outside a tree.
        }
    }

    public override string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>(base.GetResourceConfigurationWarnings());
        if (ArriveBand != null && RangeBandResource.UnusableWarning(ArriveBand, "Arrive Band") is { } unusable)
        {
            warnings.Add(unusable);
        }
        return warnings.ToArray();
    }

    public override string[] GetAgentWarnings(Node agent)
    {
        var warnings = new List<string>(base.GetAgentWarnings(agent));
        var missing = ConfigWarnings.RequireEntitySibling<AINavigator3D>(agent,
            "Navmesh pursuit needs an AINavigator3D on this entity.");
        if (missing != null) { warnings.Add(missing); }
        return warnings.ToArray();
    }

    /// <summary>Per-agent pursuit state: the navigator, the first-request wait and the no-navigator log latch.</summary>
    public sealed class NavApproachRuntime : TargetRuntime
    {
        public AINavigator3D? Navigator;
        public bool PendingFirstRequest;
        public bool WarnedNoNavigator;
    }

    #region Test Helpers
#if TOOLS
    internal void SetArriveBandForTest(RangeBandResource? band) => ArriveBand = band;
#endif
    #endregion
}
