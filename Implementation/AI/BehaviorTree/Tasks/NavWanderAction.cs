namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System;
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

    private AINavigator3D? _navAgent;
    private Vector3 _zoneCenter;
    private bool _navActive;
    private bool _pendingFirstTarget;

    /// <summary>Protected accessors for subclass target selection overrides.</summary>
    protected AINavigator3D? NavAgent => _navAgent;
    protected ZoneShape3D? TargetZone => _targetZone;
    protected Vector3 ZoneCenter => _zoneCenter;
    protected int MaxTargetAttempts => _maxTargetAttempts;

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
            _pendingFirstTarget = false;
            PickNewTarget();
            return;
        }

        if (_navAgent.IsNavigationFinished() || IsTargetReached())
        {
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
    /// Picks a new navigation target within the configured zone. Override in subclasses
    /// to customize target selection (e.g., history-aware search patterns).
    /// </summary>
    protected virtual void PickNewTarget()
    {
        if (_navAgent == null || _targetZone == null) { return; }

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
        // BB-first: production entities register on BB during init
        if (BB.TryGet<AINavigator3D>(BBDataSig.AINavComp, out var fromBB) && fromBB != null)
        {
            _navAgent = fromBB;
            return;
        }

        // Fallback: direct child lookup
        if (Agent.TryGetFirstChildOfType(out AINavigator3D? fromChild) && fromChild != null)
        {
            _navAgent = fromChild;
            return;
        }

        _navAgent = null;
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
#endif
    #endregion
}
