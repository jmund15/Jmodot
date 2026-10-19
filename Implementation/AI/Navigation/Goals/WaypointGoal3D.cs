namespace Jmodot.Implementation.AI.Navigation.Goals;

using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Godot;
using Implementation.AI.BB;
using Implementation.Shared;
using Jmodot.AI.Navigation;

/// <summary>
/// Walks to points a waypoint strategy picks, one after another: the strategy decides where, the
/// goal decides when to pick again. Hosted by a MoveAction, whose style terms (wander, zone
/// boundary) shape the walk but never end it. Without a navigator the step still runs its style
/// terms and picks nothing.
/// </summary>
[GlobalClass, Tool]
public partial class WaypointGoal3D : MovementGoal3D
{
    /// <summary>
    /// How this step picks points to walk to. Required: with no strategy the step never picks, so
    /// the Inspector flags the empty slot.
    /// </summary>
    [Export] public WaypointSelectionStrategy? Strategy { get; private set; }

    /// <summary>Metres from the picked point at which it counts as reached.</summary>
    [Export(PropertyHint.Range, "0.5,10.0,0.1")] public float ReachDistance { get; private set; } = 1.5f;

    /// <summary>
    /// On: reaching the point succeeds the step. Off: pick another point and keep walking; a
    /// condition or the state ends the step.
    /// </summary>
    [Export] public bool SucceedOnReach { get; private set; }

    /// <summary>
    /// Seconds to wait after a pick fails before trying again, so a bad zone does not spam a pick
    /// every tick.
    /// </summary>
    [Export(PropertyHint.Range, "0.5,5.0,0.1")] public float PickRetryDelay { get; private set; } = 1.0f;

    /// <summary>
    /// Seconds between new picks while walking. 0 picks only on arrival or when the path finishes.
    /// Use a positive value for a point that must follow a moving target.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,60.0,0.1")] public float RepickInterval { get; private set; }

    public override bool EndsItself => SucceedOnReach;

    public override AIConsiderationRuntime? CreateRuntime(IBlackboard? bb)
    {
        var runtime = new WaypointRuntime();
        runtime.Rng = EntityRngResolver.Resolve(bb, SeedKinds.ZoneShape, this, ref runtime.WarnedNoSeed);
        return runtime;
    }

    public override void OnEnter(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        if (runtime is not WaypointRuntime rt) { return; }

        ctx.BB.TryGet<AINavigator3D>(BBDataSig.AINavComp, out var nav);
        rt.Navigator = nav;
        rt.PickFailed = false;
        rt.RetryTimer = 0f;
        rt.SinceLastPick = 0f;

        if (nav != null && Strategy != null)
        {
            if (!rt.OriginCaptured)
            {
                rt.Origin = ctx.AgentPosition;
                rt.OriginCaptured = true;
            }
            rt.PendingFirstTarget = true;
        }
        else
        {
            rt.PendingFirstTarget = false;
        }
    }

    public override GoalStatus Advance(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        if (runtime is not WaypointRuntime rt || rt.Navigator is not { } nav || Strategy == null)
        {
            return GoalStatus.Running;
        }

        if (rt.PendingFirstTarget)
        {
            if (!nav.IsMapReady()) { return GoalStatus.Running; }
            rt.PendingFirstTarget = false;
            PickNewTarget(ctx, rt, nav);
            return GoalStatus.Running;
        }

        if (rt.PickFailed)
        {
            rt.RetryTimer -= ctx.Delta;
            if (rt.RetryTimer > 0f) { return GoalStatus.Running; }
            rt.PickFailed = false;
        }

        if (RepickInterval > 0f)
        {
            rt.SinceLastPick += ctx.Delta;
            if (rt.SinceLastPick >= RepickInterval)
            {
                PickNewTarget(ctx, rt, nav);
                return GoalStatus.Running;
            }
        }

        if (nav.IsNavigationFinished() || ctx.AgentPosition.DistanceTo(nav.TargetPosition) < ReachDistance)
        {
            if (SucceedOnReach) { return GoalStatus.Succeeded; }
            PickNewTarget(ctx, rt, nav);
        }
        return GoalStatus.Running;
    }

    public override void OnExit(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        if (runtime is not WaypointRuntime rt) { return; }

        rt.Navigator?.ClearPath();
        rt.PendingFirstTarget = false;
    }

    public override string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>(base.GetResourceConfigurationWarnings());
        warnings.AddRange(ConfigWarnings.ResourceSlotWarnings("Strategy", Strategy, required: true));
        if (!(ReachDistance > 0f) || float.IsInfinity(ReachDistance))
        {
            warnings.Add($"Reach Distance ({ReachDistance}) must be a positive, finite number of metres.");
        }
        if (!(PickRetryDelay >= 0f) || float.IsInfinity(PickRetryDelay))
        {
            warnings.Add($"Pick Retry Delay ({PickRetryDelay}) must be zero or a positive, finite number of seconds.");
        }
        if (!(RepickInterval >= 0f) || float.IsInfinity(RepickInterval))
        {
            warnings.Add($"Repick Interval ({RepickInterval}) must be zero or a positive, finite number of seconds.");
        }
        return warnings.ToArray();
    }

    private void PickNewTarget(in MovementGoalContext ctx, WaypointRuntime rt, AINavigator3D nav)
    {
        rt.SinceLastPick = 0f;
        var context = new WaypointContext(rt.Origin, ctx.AgentPosition, ctx.BB, rt.Rng);
        if (Strategy!.TrySelectTarget(nav, context, rt.History)) { return; }

        JmoLogger.Warning(this, "WaypointStrategy failed to find target.");
        rt.PickFailed = true;
        rt.RetryTimer = PickRetryDelay;
    }

    /// <summary>Per-agent pick state: origin, history, retry clock and the zone-shape stream.</summary>
    public sealed class WaypointRuntime : AIConsiderationRuntime
    {
        public Vector3 Origin;
        public bool OriginCaptured;
        public readonly Queue<Vector3> History = new();
        public JmoRng Rng = null!;
        public AINavigator3D? Navigator;
        public bool PendingFirstTarget;
        public bool PickFailed;
        public float RetryTimer;
        public float SinceLastPick;
        public bool WarnedNoSeed;

        #region Test Helpers
#if TOOLS
        /// <summary>Clears the wait for IsMapReady(), which a Logic host never reaches.</summary>
        internal void ClearPendingFirstTarget() => PendingFirstTarget = false;
#endif
        #endregion
    }

    #region Test Helpers
#if TOOLS
    internal void SetStrategyForTest(WaypointSelectionStrategy? strategy) => Strategy = strategy;
    internal void SetReachDistanceForTest(float value) => ReachDistance = value;
    internal void SetSucceedOnReachForTest(bool value) => SucceedOnReach = value;
    internal void SetPickRetryDelayForTest(float value) => PickRetryDelay = value;
    internal void SetRepickIntervalForTest(float value) => RepickInterval = value;
#endif
    #endregion
}
