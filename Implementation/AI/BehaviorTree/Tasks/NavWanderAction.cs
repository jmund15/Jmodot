namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System;
using System.Collections.Generic;
using BB;
using Core.AI;
using Core.AI.Navigation.Zones;
using Navigation;
using Jmodot.AI.Navigation;
using Shared;

/// <summary>
/// A SteeringBehaviorAction that adds navigation target lifecycle on top of
/// consideration management. Picks random target points within a zone,
/// feeds them to an AINavigator3D for pathfinding, and lets considerations
/// (wander + zone boundary + nav path) blend via score summation.
///
/// For aimless wandering without navigation targets, use SteeringBehaviorAction
/// directly with WanderConsideration3D + ZoneBoundaryConsideration3D.
///
/// Graceful degradation: when no AINavigator3D is present, behaves identically
/// to SteeringBehaviorAction (considerations register, no nav targets).
/// </summary>
[GlobalClass, Tool]
public partial class NavWanderAction : SteeringBehaviorAction
{
    [ExportGroup("Navigation")]
    [Export] private ZoneShape3D? _targetZone;

    [Export(PropertyHint.Range, "0.5, 10.0, 0.1")]
    private float _targetReachDistance = 1.5f;

    [Export(PropertyHint.Range, "1, 20, 1")]
    private int _maxTargetAttempts = 5;

    /// <summary>
    /// When true, the action succeeds (TaskStatus.Success) upon reaching its target
    /// instead of picking a new one. Useful for wander-then-idle sequences.
    /// </summary>
    [Export] private bool _succeedOnReach = false;

    /// <summary>
    /// Optional composable waypoint selection strategy. When set, delegates target
    /// selection to the strategy instead of using default random sampling.
    /// </summary>
    [Export] private WaypointSelectionStrategy? _waypointStrategy;

    private AINavigator3D? _navAgent;
    private Vector3 _zoneCenter;
    private bool _navActive;
    private bool _pendingFirstTarget;
    private readonly Queue<Vector3> _waypointHistory = new();

    protected override void OnEnter()
    {
        base.OnEnter();

        if (Status == TaskStatus.Failure) { return; }

        ResolveNavAgent();

        if (_navAgent != null && _targetZone != null)
        {
            _zoneCenter = ((Node3D)Agent).GlobalPosition;
            _navActive = true;
            _pendingFirstTarget = true;
        }
        else
        {
            _navActive = false;
        }
    }

    protected override void OnProcessPhysics(float delta)
    {
        if (!_navActive || _navAgent == null) { return; }

        if (_pendingFirstTarget)
        {
            if (!_navAgent.IsMapReady()) { return; }
            _pendingFirstTarget = false;
            PickNewTarget();
            return;
        }

        if (_navAgent.IsNavigationFinished() || IsTargetReached())
        {
            if (_succeedOnReach)
            {
                Status = TaskStatus.Success;
                return;
            }
            PickNewTarget();
        }
    }

    protected override void OnExit()
    {
        if (_navAgent != null)
        {
            TryClearNavPath();
        }

        _navActive = false;
        _pendingFirstTarget = false;

        base.OnExit();
    }

    /// <summary>
    /// Picks a new navigation target within the configured zone. Delegates to
    /// the waypoint strategy when set, otherwise uses default random sampling.
    /// </summary>
    private void PickNewTarget()
    {
        if (_navAgent == null || _targetZone == null) { return; }

        if (_waypointStrategy != null)
        {
            var context = new WaypointContext(_zoneCenter, ((Node3D)Agent).GlobalPosition, BB);
            if (!_waypointStrategy.TrySelectTarget(_navAgent, context, _waypointHistory))
            {
                JmoLogger.Warning(this, "WaypointStrategy failed to find target.");
            }
            return;
        }

        for (int i = 0; i < _maxTargetAttempts; i++)
        {
            Vector3 candidate = _targetZone.SampleRandomInteriorPoint(_zoneCenter);
            var response = _navAgent.RequestNewNavPath(candidate, overridePathCalcThresh: 0f);
            if (response == NavReqPathResponse.Success)
            {
                return;
            }
        }

        JmoLogger.Warning(this, $"Failed to find reachable target after {_maxTargetAttempts} attempts — staying on current path.");
    }

    private bool IsTargetReached()
    {
        float dist = ((Node3D)Agent).GlobalPosition.DistanceTo(_navAgent!.TargetPosition);
        return dist < _targetReachDistance;
    }

    private void ResolveNavAgent()
    {
        BB.TryGet<AINavigator3D>(BBDataSig.AINavComp, out var nav);
        _navAgent = nav;
    }

    private void TryClearNavPath()
    {
        try
        {
            _navAgent!.ClearPath();
        }
        catch (NullReferenceException)
        {
            // ClearPath accesses _ownerAgent which may be null in test contexts
            // where AINavigator3D._Ready() never ran. Safe to ignore.
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetTargetZone(ZoneShape3D zone) => _targetZone = zone;
    internal void SetTargetReachDistance(float distance) => _targetReachDistance = distance;
    internal void SetMaxTargetAttempts(int attempts) => _maxTargetAttempts = attempts;
    internal bool IsNavPendingFirstTarget() => _pendingFirstTarget;
    internal bool IsSucceedOnReach() => _succeedOnReach;
    internal void SetSucceedOnReach(bool value) => _succeedOnReach = value;
    internal WaypointSelectionStrategy? GetWaypointStrategy() => _waypointStrategy;
    internal Queue<Vector3> GetWaypointHistory() => _waypointHistory;
#endif
    #endregion
}
