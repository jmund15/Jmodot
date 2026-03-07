namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System;
using System.Collections.Generic;
using BB;
using Core.AI;
using Navigation;
using Jmodot.AI.Navigation;
using Shared;

/// <summary>
/// A SteeringBehaviorAction that adds waypoint lifecycle management on top of
/// consideration management. Delegates target selection to a WaypointSelectionStrategy,
/// detects target reach, and repeats.
///
/// For aimless wandering without navigation targets, use SteeringBehaviorAction
/// directly with WanderConsideration3D + ZoneBoundaryConsideration3D.
///
/// Graceful degradation: when no AINavigator3D is present, behaves identically
/// to SteeringBehaviorAction (considerations register, no nav targets).
/// </summary>
[GlobalClass, Tool]
public partial class WaypointSteeringAction : SteeringBehaviorAction
{
    [ExportGroup("Navigation")]

    /// <summary>
    /// Required waypoint selection strategy. Handles where and how to pick targets.
    /// The action handles when (on enter, on reach, on nav finish).
    /// </summary>
    [Export] private WaypointSelectionStrategy? _waypointStrategy;

    [Export(PropertyHint.Range, "0.5, 10.0, 0.1")]
    private float _targetReachDistance = 1.5f;

    /// <summary>
    /// When true, the action succeeds (TaskStatus.Success) upon reaching its target
    /// instead of picking a new one. Useful for wander-then-idle sequences.
    /// </summary>
    [Export] private bool _succeedOnReach = false;

    [ExportGroup("Retry")]

    /// <summary>
    /// When PickNewTarget fails, wait this many seconds before retrying.
    /// Prevents log spam when waypoint selection repeatedly fails (e.g., zone exceeds nav mesh).
    /// </summary>
    [Export(PropertyHint.Range, "0.5, 5.0, 0.1")]
    private float _pickRetryDelay = 1.0f;

    private AINavigator3D? _navAgent;
    private Vector3 _originPosition;
    private bool _originCaptured;
    private bool _navActive;
    private bool _pendingFirstTarget;
    private bool _pickFailed;
    private float _retryTimer;
    private readonly Queue<Vector3> _waypointHistory = new();

    protected override void OnEnter()
    {
        base.OnEnter();

        if (Status == TaskStatus.Failure) { return; }

        ResolveNavAgent();

        _pickFailed = false;
        _retryTimer = 0f;

        if (_navAgent != null && _waypointStrategy != null)
        {
            if (!_originCaptured)
            {
                _originPosition = ((Node3D)Agent).GlobalPosition;
                _originCaptured = true;
            }
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

        if (_pickFailed)
        {
            _retryTimer -= delta;
            if (_retryTimer > 0f) { return; }
            _pickFailed = false;
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

    private void PickNewTarget()
    {
        if (_navAgent == null || _waypointStrategy == null) { return; }

        var context = new WaypointContext(_originPosition, ((Node3D)Agent).GlobalPosition);
        if (!_waypointStrategy.TrySelectTarget(_navAgent, context, _waypointHistory))
        {
            JmoLogger.Warning(this, "WaypointStrategy failed to find target.");
            _pickFailed = true;
            _retryTimer = _pickRetryDelay;
        }
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
    internal void SetWaypointStrategy(WaypointSelectionStrategy strategy) => _waypointStrategy = strategy;
    internal void SetTargetReachDistance(float distance) => _targetReachDistance = distance;
    internal bool IsNavPendingFirstTarget() => _pendingFirstTarget;
    internal bool IsSucceedOnReach() => _succeedOnReach;
    internal void SetSucceedOnReach(bool value) => _succeedOnReach = value;
    internal WaypointSelectionStrategy? GetWaypointStrategy() => _waypointStrategy;
    internal Queue<Vector3> GetWaypointHistory() => _waypointHistory;
    internal Vector3 GetOriginPosition() => _originPosition;
    internal void SetOriginPositionForTest(Vector3 pos) => _originPosition = pos;
    internal void SetPickRetryDelay(float delay) => _pickRetryDelay = delay;
    internal bool IsPickCooldownActive() => _pickFailed;
    /// <summary>
    /// Bypasses the IsMapReady() gate and directly triggers the first PickNewTarget().
    /// Required because NavigationServer3D is never ready in unit tests.
    /// </summary>
    internal void ForceFirstTargetPick()
    {
        _pendingFirstTarget = false;
        PickNewTarget();
    }
#endif
    #endregion
}
