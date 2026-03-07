namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using Core.AI.Navigation.Zones;

/// <summary>
/// Abstract base for composable waypoint selection strategies. Implementations
/// define how a waypoint action picks its next navigation target.
/// Designed as a Resource so strategies can be configured per-entity in the Inspector.
///
/// Zone and retry budget are optional shared exports — strategies that need a zone
/// use it via <see cref="SampleCandidate"/>; strategies without a zone get random
/// nav mesh points instead.
/// </summary>
[GlobalClass, Tool]
public abstract partial class WaypointSelectionStrategy : Resource
{
    /// <summary>
    /// Optional zone shape for constraining candidate waypoints.
    /// When null, <see cref="SampleCandidate"/> falls back to random nav mesh points.
    /// </summary>
    [Export] private ZoneShape3D? _zone;

    /// <summary>
    /// Shared retry budget for candidate sampling loops.
    /// </summary>
    [Export(PropertyHint.Range, "1, 20, 1")]
    private int _maxAttempts = 5;

    protected ZoneShape3D? Zone => _zone;
    protected int MaxAttempts => _maxAttempts;

    /// <summary>
    /// Samples a candidate waypoint. Delegates to zone if configured,
    /// otherwise falls back to a random point on the navigation mesh.
    /// </summary>
    protected Vector3 SampleCandidate(AINavigator3D nav, WaypointContext context)
    {
        if (_zone != null)
        {
            return _zone.SampleRandomInteriorPoint(context.OriginPosition);
        }
        return nav.SampleRandomNavPoint();
    }

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

    #region Test Helpers
#if TOOLS
    internal void SetZone(ZoneShape3D? zone) => _zone = zone;
    internal void SetMaxAttempts(int value) => _maxAttempts = value;
    internal ZoneShape3D? GetZone() => _zone;
    internal int GetMaxAttempts() => _maxAttempts;
    internal Vector3 SampleCandidateForTest(AINavigator3D nav, WaypointContext context)
        => SampleCandidate(nav, context);
#endif
    #endregion
}
