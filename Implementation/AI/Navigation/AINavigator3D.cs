namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.AI.Navigation;
using Shared;

/* TODO:
 * Instead of a fixed timer, the AINavigator should recalculate its path based on specific conditions:
 * When the target moves too far: The navigator should store the position where it last calculated a path to (_lastCalculatedTargetPath). Every frame, it checks the distance between this stored position and the actual target's current position. If this distance exceeds a threshold (e.g., 2 meters), it's time to recalculate. This makes the AI highly responsive.
 * When the path is invalidated: If the current path becomes blocked (e.g., a dynamic door closes), the agent will get stuck. The navigator can detect this (e.g., if Velocity.IsZeroApprox() for more than a second while IsNavigationFinished() is false) and trigger a recalculation.
 * When the TargetReached signal is emitted: The navigator should stop all pathfinding until it is given a new target.
 * This conditional logic provides the same performance benefits as a timer but makes the AI far more responsive and efficient.
 */

/// <summary>
///     A pure "driver" component responsible for low-level agent movement. It takes a desired
///     steering direction from the AISteeringProcessor and uses Godot's NavigationAgent3D
///     to execute the final movement, handling pathfinding and velocity updates. It has no
///     knowledge of why it is moving, only how.
/// </summary>
[Tool]
[GlobalClass]
public partial class AINavigator3D : NavigationAgent3D
{
    private NavigationProfile _activeProfile;

    private Vector3 _lastCalculatedTargetPath;

    /// <summary>
    ///     Gets the agent's current linear velocity.
    /// </summary>
    //public Vector3 Velocity { get; private set; }
    [Export(PropertyHint.Range, "0, 50, 0.1")]
    private float _maxSpeed = 10.0f;

    private Node3D _ownerAgent = null!;

    /// <summary>
    ///     This variable represents the navigator's responses to new path requests (in meters).
    ///     Only path requests that are this distance or further from the current target will be accepted.
    ///     A larger value will increase performance while decreasing target accuracy. This can be overriden in the RequestPath
    ///     function.
    /// </summary>
    [Export]
    public float DefaultPathCalculationThreshold { get; private set; } = 1.0f;

    public override string[] _GetConfigurationWarnings()
    {
        if (this.GetOwnerOrNull<Node3D>() == null)
        {
            return new[] { "AINavigator3D must be owned an Node3D node for proper coordination." };
        }

        return base._GetConfigurationWarnings();
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        this._ownerAgent = this.GetOwnerOrNull<Node3D>();
        if (this._ownerAgent == null)
        {
            JmoLogger.Error(this, "AINavigator3D's owner is not of type Node3D", this.GetOwnerOrNull<Node>());
        }

        // Ensure the agent doesn't try to move itself. The parent body is the one that moves.
        // This is a common point of confusion with NavigationAgent3D.
        this.VelocityComputed += this.OnVelocityComputed;
    }

    /// <summary>
    ///     Sets the target position for the navigation agent.
    ///     Returns true if the path request is accepted, false if on cooldown.
    /// </summary>
    /// <param name="globalPosition">The global position to navigate to.</param>
    /// <param name="overridePathCalcThresh">
    ///     If set, overrides the default path calc threshold. Set to zero or less to
    ///     eliminate this calculation entirely.
    /// </param>
    /// <returns>Boolean indicating if the request was successful.</returns>
    public NavReqPathResponse RequestPath(Vector3 globalPosition, float? overridePathCalcThresh = null)
    {
        var calcThreshold = overridePathCalcThresh ?? this.DefaultPathCalculationThreshold;
        if (calcThreshold > 0f && this.TargetPosition.DistanceTo(globalPosition) < calcThreshold)
        {
            return NavReqPathResponse.TooCloseToPrevTarget;
        }

        // TODO: replace with a better calculation of what the current map the agent is actually in, instead of iterating through all of them.
        var rIDs = NavigationServer3D.GetMaps();
        foreach (var rID in rIDs)
        {
            var mapDist = NavigationServer3D.MapGetClosestPoint(rID, globalPosition).DistanceTo(globalPosition);
            var mapDistAllowance = 1f; // 0.01f
            //bool isNavMesh = mapDist <= float.Epsilon; //if point is in a nav region, its distance should be ~0.0
            if (mapDist <= mapDistAllowance)
            {
                this.TargetPosition = globalPosition; //Allow for path to be set if it is on a nav mesh
                return NavReqPathResponse.Success;
            }
        }

        return NavReqPathResponse.Unreachable;
    }

    /// <summary>
    ///     Sets the desired movement direction for the current frame. The navigator will attempt
    ///     to move in this direction, respecting pathfinding and physics.
    /// </summary>
    /// <param name="direction">The normalized direction vector for movement.</param>
    public void SetMovementDirection(Vector3 direction)
    {
        if (!this.IsNavigationFinished())
        {
            var nextPathPos = this.GetNextPathPosition();
            var navDirection = this._ownerAgent.GlobalPosition.DirectionTo(nextPathPos);
            // Simple blend between desired direction and navigation path direction
            var finalDirection = direction.Lerp(navDirection, 0.5f).Normalized();
            this.SetVelocity(finalDirection * this._maxSpeed);
        }
        else
        {
            this.SetVelocity(direction * this._maxSpeed);
        }
    }

    private void OnVelocityComputed(Vector3 safeVelocity)
    {
        this.Velocity = safeVelocity;
        // The owning Node3D (which must be a CharacterBody3D or similar) is responsible for actually moving. This signal connection is key.
        // In your CharacterBody3D script: navigator.VelocityComputed += (vel) => SetVelocity(vel); MoveAndSlide();
    }

    #region HELPER_FUNCTIONS

    public Vector3 GetIdealDirection()
    {
        if (this.IsNavigationFinished())
        {
            return Vector3.Zero;
        }

        var nextPathPos = this.GetNextPathPosition();
        return this._ownerAgent.GlobalPosition.DirectionTo(nextPathPos);
    }

    /// <summary>
    ///     Changes the agent's active navigation profile at runtime.
    /// </summary>
    public void SetNavigationProfile(NavigationProfile newProfile)
    {
        if (newProfile == null)
        {
            JmoLogger.Error(this, "Attempted to set a null NavigationProfile.", this.GetOwnerOrNull<Node>());
            return;
        }

        this._activeProfile = newProfile;
        this.SetNavigationLayers(this._activeProfile.NavigationLayers);
    }

    /// <summary>
    ///     Calculates the total length of the current navigation path.
    ///     This is from beginning to end, regardless of the agents current position in the path
    /// </summary>
    /// <returns>The path distance in meters, or zero if no path exists.</returns>
    public float GetTotalPathDistance()
    {
        if (this.IsNavigationFinished())
        {
            return 0f;
        }

        var pathPoints = this.GetCurrentNavigationPath();
        if (pathPoints.Length < 2)
        {
            return 0f;
        }

        var distance = this._ownerAgent.GlobalPosition.DistanceTo(pathPoints[0]);
        for (var i = 0; i < pathPoints.Length - 1; i++)
        {
            distance += pathPoints[i].DistanceTo(pathPoints[i + 1]);
        }

        return distance;
    }

    /// <summary>
    ///     Calculates the remaining distance along the current navigation path.
    /// </summary>
    /// <returns>The path distance to the target in meters.</returns>
    public float GetRemainingPathDistance()
    {
        if (this.IsNavigationFinished())
        {
            return 0f;
        }

        var pathPoints = this.GetCurrentNavigationPath();
        var currentIndex = this.GetCurrentNavigationPathIndex();
        if (currentIndex >= pathPoints.Length)
        {
            return 0f;
        }

        var distance = this._ownerAgent.GlobalPosition.DistanceTo(pathPoints[currentIndex]);

        for (var i = currentIndex; i < pathPoints.Length - 1; i++)
        {
            distance += pathPoints[i].DistanceTo(pathPoints[i + 1]);
        }

        return distance;
    }

    /// <summary>
    ///     Finds the target node from a list that is closest via navigation path distance.
    /// </summary>
    /// <remarks>
    ///     This is a synchronous operation and a potentially expensive call.
    ///     It may cause frame hitches if the target list is large.
    ///     ONLY USE WITH SMALL TARGET LISTS OR DURING NON-REALTIME SCENARIOS.
    /// </remarks>
    /// <param name="targets">A list of potential target nodes.</param>
    /// <returns>The closest reachable target, or null if none are reachable.</returns>
    public Node3D? FindNearestNavTargetSyncronous(IEnumerable<Node3D> targets, bool optimize = true)
    {
        Node3D? closestTarget = null;
        var shortestDistance = float.MaxValue;
        var map = this.GetNavigationMap();

        foreach (var target in targets)
        {
            // Use the NavigationServer directly for a synchronous path query.
            var path = NavigationServer3D.MapGetPath(map, this._ownerAgent.GlobalPosition, target.GlobalPosition,
                optimize, this._activeProfile.NavigationLayers);

            if (path.Length > 0)
            {
                var distance = this.CalculatePathLength(path);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    closestTarget = target;
                }
            }
        }

        return closestTarget;
    }

    public async Task<Node3D?> FindNearestNavTargetAsync(IEnumerable<Node3D> targets,
        CancellationToken cancellationToken, bool optimize = true)
    {
        Node3D? closestTarget = null;
        var shortestDistance = float.MaxValue;
        var map = this.GetNavigationMap();

        foreach (var target in targets)
        {
            // Check if the task was cancelled from outside
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            //NavigationServer3D.QueryPath // TODO: look into using this instead
            var path = NavigationServer3D.MapGetPath(map, this._ownerAgent.GlobalPosition, target.GlobalPosition,
                optimize, this._activeProfile.NavigationLayers);

            if (path.Length > 0)
            {
                var distance = this.CalculatePathLength(path);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    closestTarget = target;
                }
            }

            // Wait for the next frame before starting the next expensive query.
            await this.ToSignal(this.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        return closestTarget;
    }

    private float CalculatePathLength(Vector3[] path)
    {
        var distance = 0f;
        for (var i = 0; i < path.Length - 1; i++)
        {
            distance += path[i].DistanceTo(path[i + 1]);
        }

        return distance;
    }

    #endregion
}
