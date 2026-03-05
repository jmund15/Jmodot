namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;

/// <summary>
/// Abstract base for composable waypoint selection strategies. Implementations
/// define how a waypoint action picks its next navigation target.
/// Designed as a Resource so strategies can be configured per-entity in the Inspector.
///
/// Strategies own their own zone/configuration. The action provides context
/// (origin, current position) and the navigator for pathfinding.
/// </summary>
[GlobalClass, Tool]
public abstract partial class WaypointSelectionStrategy : Resource
{
    /// <summary>
    /// Attempts to select and set a new navigation target on the navigator.
    /// </summary>
    /// <param name="nav">The navigator to set the target on.</param>
    /// <param name="context">Contextual data from the waypoint action (origin, current position).</param>
    /// <param name="waypointHistory">Persistent history queue owned by the caller.</param>
    /// <returns>True if a valid target was found and set, false otherwise.</returns>
    public abstract bool TrySelectTarget(
        AINavigator3D nav, WaypointContext context,
        Queue<Vector3> waypointHistory);
}
