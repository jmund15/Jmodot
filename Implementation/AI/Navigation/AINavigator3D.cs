namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.AI.Navigation;
using Shared;


/// <summary>
/// A pure "driver" component responsible for low-level agent movement. It uses Godot's
/// NavigationAgent3D to handle pathfinding and avoidance. Its primary job is to accept
/// a high-level desired velocity and translate it into a safe velocity for the parent
/// physics body to use. It has no knowledge of *why* it is moving, only *how*.
/// </summary>
[Tool]
[GlobalClass]
public partial class AINavigator3D : NavigationAgent3D
{
    private Node3D _ownerAgent = null!;
    private NavigationProfile _activeProfile= null!;

    [ExportGroup("Movement")] [Export(PropertyHint.Range, "0, 50, 0.1")]
    private float _maxSpeed = 10.0f;

    [ExportGroup("Pathing Behavior")]

    /// <summary>
    /// To prevent excessive path requests, new targets within this distance (in meters) of the
    /// current target will be ignored. This can be overridden in the RequestPath function.
    /// </summary>
    [Export] public float DefaultPathCalculationThreshold { get; private set; } = 1.0f;

    /// <summary>
    /// If the current target moves further than this distance from where the last path was
    /// calculated, a new path will be automatically requested. This makes the agent
    /// responsive to moving targets.
    /// </summary>
    [Export(PropertyHint.Range, "0.5, 10.0, 0.1")]
    public float RecalculateDistanceThreshold { get; private set; } = 2.0f;

    /// <summary>
    /// If the agent's velocity is near zero for more than this duration (in seconds) while it
    /// has not reached its target, it's considered stuck and will request a new path.
    /// </summary>
    [Export(PropertyHint.Range, "0.5, 5.0, 0.1")]
    public float StuckTimeThreshold { get; private set; } = 1.0f;

    private Vector3 _lastCalculatedTargetPath;
    private double _stuckTimer = 0.0;

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (GetOwnerOrNull<CharacterBody3D>() == null)
        {
            warnings.Add(
                "AINavigator3D should be a child of a CharacterBody3D (or other physics body) to function correctly.");
        }

        return warnings.ToArray();
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        _ownerAgent = GetOwner<Node3D>();

        // This component is useless without an owner.
        if (_ownerAgent == null)
        {
            JmoLogger.Error(this, "Owner is not a Node3D. Disabling component.");
            SetPhysicsProcess(false);
            return;
        }

        // Connect to signals for automatic state handling.
        TargetReached += OnTargetReached;
    }

    public override void _PhysicsProcess(double delta)
    {
        // If we have no path, there's nothing to check.
        if (IsNavigationFinished())
        {
            _stuckTimer = 0.0;
            return;
        }

        // Condition 1: Target has moved too far from where we last pathed.
        // This is crucial for making the AI responsive to moving targets.
        if (TargetPosition.DistanceTo(_lastCalculatedTargetPath) > RecalculateDistanceThreshold)
        {
            RequestPath(TargetPosition);
        }

        // Condition 2: Agent is stuck (e.g., a door closed on its path).
        if (Velocity.IsZeroApprox())
        {
            _stuckTimer += delta;
            if (_stuckTimer > StuckTimeThreshold)
            {
                JmoLogger.Info(this, "Agent appears to be stuck. Recalculating path.");
                RequestPath(TargetPosition); // Recalculate path to the same target.
                _stuckTimer = 0.0; // Reset timer after recalculating.
            }
        }
        else
        {
            _stuckTimer = 0.0; // We are moving, so we are not stuck.
        }
    }

    /// <summary>
    /// Sets the target position for the navigation agent. This is the primary method
    /// for commanding the agent to move to a new location.
    /// </summary>
    /// <param name="globalPosition">The global position to navigate to.</param>
    /// <param name="overridePathCalcThresh">If set, overrides the default path calculation threshold.</param>
    public NavReqPathResponse RequestPath(Vector3 globalPosition, float? overridePathCalcThresh = null)
    {
        float calcThreshold = overridePathCalcThresh ?? DefaultPathCalculationThreshold;
        if (calcThreshold > 0f && TargetPosition.DistanceTo(globalPosition) < calcThreshold)
        {
            return NavReqPathResponse.TooCloseToPrevTarget;
        }

        // Check if the target point is on a valid navigation mesh.
        // Using GetNavigationMap() is more direct than iterating all maps.
        Rid map = GetNavigationMap();
        Vector3 closestPointOnNavmesh = NavigationServer3D.MapGetClosestPoint(map, globalPosition);

        // Allow for a small tolerance in case the target is slightly off the mesh.
        if (closestPointOnNavmesh.DistanceTo(globalPosition) <= 1.0f)
        {
            TargetPosition = globalPosition;
            _lastCalculatedTargetPath = globalPosition; // Store this position for future checks.
            _stuckTimer = 0.0; // Reset stuck timer on new path.
            return NavReqPathResponse.Success;
        }

        return NavReqPathResponse.Unreachable;
    }

    /// <summary>
    /// This is the core update method. The AIAgent calls this every frame, providing the
    /// final desired direction from the steering system. This method sets the agent's
    /// desired velocity for the Godot NavigationServer to process.
    /// </summary>
    /// <param name="steeringDirection">The normalized direction vector from the AISteeringProcessor.</param>
    public void UpdateVelocity(Vector3 steeringDirection)
    {
        if (IsNavigationFinished())
        {
            // If there's no path, we just use the steering direction directly.
            // This is useful for wandering, local avoidance, etc.
            Velocity = steeringDirection * _maxSpeed;
        }
        else
        {
            // If we are on a path, blend the strict path direction with the desired steering direction.
            // This allows the AI to follow a path while still reacting to its environment (e.g., dodging).
            Vector3 nextPathPos = GetNextPathPosition();
            Vector3 navDirection = _ownerAgent.GlobalPosition.DirectionTo(nextPathPos);

            // A 50/50 Lerp is a good default starting point for blending.
            Vector3 finalDirection = steeringDirection.Lerp(navDirection, 0.5f).Normalized();
            Velocity = finalDirection * _maxSpeed;
        }
    }

    /// <summary>
    /// Called when the agent reaches the end of its current path.
    /// </summary>
    private void OnTargetReached()
    {
        // The canonical way to stop a NavigationAgent is to set its target to its own position.
        TargetPosition = _ownerAgent.GlobalPosition;
        _lastCalculatedTargetPath = _ownerAgent.GlobalPosition;
        JmoLogger.Info(this, "Target reached. Halting navigation.");
    }

    #region HELPER_FUNCTIONS

    /// <summary>
    /// Gets the ideal, straight-line direction to the next corner of the current navigation path.
    /// This is used by the steering system as the "goal" direction.
    /// </summary>
    public Vector3 GetIdealDirection()
    {
        if (IsNavigationFinished())
        {
            return Vector3.Zero;
        }

        Vector3 nextPathPos = GetNextPathPosition();
        return _ownerAgent.GlobalPosition.DirectionTo(nextPathPos);
    }

    /// <summary>
    /// Changes the agent's active navigation profile at runtime.
    /// </summary>
    public void SetNavigationProfile(NavigationProfile newProfile)
    {
        _activeProfile = newProfile;
        SetNavigationLayers(_activeProfile.NavigationLayers);
    }

    /// <summary>
    /// Calculates the total length of the current navigation path.
    /// This is from beginning to end, regardless of the agents current position in the path
    /// </summary>
    /// <returns>The path distance in meters, or zero if no path exists.</returns>
    public float GetTotalPathDistance()
    {
        if (IsNavigationFinished()) { return 0f; }

        Vector3[] pathPoints = GetCurrentNavigationPath();
        if (pathPoints.Length < 2) { return 0f; }

        float distance = _ownerAgent.GlobalPosition.DistanceTo(pathPoints[0]);
        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            distance += pathPoints[i].DistanceTo(pathPoints[i + 1]);
        }
        return distance;
    }

    /// <summary>
    /// Calculates the remaining distance along the current navigation path.
    /// </summary>
    /// <returns>The path distance to the target in meters.</returns>
    public float GetRemainingPathDistance()
    {
        if (IsNavigationFinished()) { return 0f; }

        Vector3[] pathPoints = GetCurrentNavigationPath();
        int currentIndex = GetCurrentNavigationPathIndex();
        if (currentIndex >= pathPoints.Length) { return 0f; }

        float distance = _ownerAgent.GlobalPosition.DistanceTo(pathPoints[currentIndex]);

        for (int i = currentIndex; i < pathPoints.Length - 1; i++)
        {
            distance += pathPoints[i].DistanceTo(pathPoints[i + 1]);
        }
        return distance;
    }

    /// <summary>
    /// Finds the target node from a list that is closest via navigation path distance.
    /// </summary>
    /// <remarks>
    /// This is a synchronous operation and a potentially expensive call.
    /// It may cause frame hitches if the target list is large.
    /// ONLY USE WITH SMALL TARGET LISTS OR DURING NON-REALTIME SCENARIOS.
    /// </remarks>
    /// <param name="targets">A list of potential target nodes.</param>
    /// <param name="optimize">Tells the NavigationServer whether to optimize the path request</param>
    /// <returns>The closest reachable target, or null if none are reachable.</returns>
    public Node3D? FindNearestNavTargetSyncronous(IEnumerable<Node3D> targets, bool optimize = true)
    {
        Node3D? closestTarget = null;
        float shortestDistance = float.MaxValue;
        Rid map = GetNavigationMap();

        foreach (var target in targets)
        {
            // Use the NavigationServer directly for a synchronous path query.
            Vector3[] path = NavigationServer3D.MapGetPath(map, _ownerAgent.GlobalPosition, target.GlobalPosition, optimize, _activeProfile.NavigationLayers);

            if (path.Length > 0)
            {
                float distance = CalculatePathLength(path);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    closestTarget = target;
                }
            }
        }
        return closestTarget;
    }

    public async Task<Node3D?> FindNearestNavTargetAsync(IEnumerable<Node3D> targets, CancellationToken cancellationToken, bool optimize = true)
    {
        Node3D? closestTarget = null;
        float shortestDistance = float.MaxValue;
        Rid map = GetNavigationMap();

        foreach (var target in targets)
        {
            // Check if the task was cancelled from outside
            if (cancellationToken.IsCancellationRequested) { return null; }

            //NavigationServer3D.QueryPath // TODO: look into using this instead
            Vector3[] path = NavigationServer3D.MapGetPath(map, _ownerAgent.GlobalPosition, target.GlobalPosition, optimize, _activeProfile.NavigationLayers);

            if (path.Length > 0)
            {
                float distance = CalculatePathLength(path);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    closestTarget = target;
                }
            }

            // Wait for the next frame before starting the next expensive query.
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        return closestTarget;
    }

    private float CalculatePathLength(Vector3[] path)
    {
        float distance = 0f;
        for (int i = 0; i < path.Length - 1; i++)
        {
            distance += path[i].DistanceTo(path[i + 1]);
        }
        return distance;
    }
    #endregion
}
