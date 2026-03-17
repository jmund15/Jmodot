namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using Core.Identification;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.AI.Perception;
using Shared;

/// <summary>
/// Waypoint selection strategy that computes a deterministic flee point away from
/// a threat position stored on the blackboard. Projects a point at _fleeDistance
/// along the flee direction, snaps it to the nav mesh via MapGetClosestPoint,
/// and validates the snap distance against _maxSnapDistance. Falls back to shorter
/// projection distances if the snap exceeds the threshold.
///
/// Designed for terminal flee actions (e.g., ScurryFadeAction) where the critter
/// picks ONE destination and navigates to it via nav-mesh-routed pathfinding.
/// </summary>
[GlobalClass, Tool]
public partial class ThreatAwareWaypointSelection : WaypointSelectionStrategy
{
    /// <summary>
    /// Category to query from AIPerceptionManager3D for threat position.
    /// When set, queries perception directly. When null, falls back to BB key.
    /// </summary>
    [Export] private Category? _threatCategory;

    /// <summary>Legacy BB key fallback. Only used when _threatCategory is null.</summary>
    [Export] private StringName _threatPositionKey = new("Critter_ThreatPosition");

    [Export(PropertyHint.Range, "5.0, 40.0, 0.5")]
    private float _fleeDistance = 20.0f;

    /// <summary>
    /// Maximum acceptable distance between the projected flee point and the closest
    /// nav mesh surface. If MapGetClosestPoint snaps further than this, the projected
    /// point is considered off-mesh and the strategy tries a shorter distance.
    /// </summary>
    [Export(PropertyHint.Range, "1.0, 15.0, 0.5")]
    private float _maxSnapDistance = 5.0f;

    private static readonly float[] FallbackMultipliers = { 1.0f, 0.75f, 0.5f };

    public override bool TrySelectTarget(
        AINavigator3D nav, WaypointContext context, Queue<Vector3> waypointHistory)
    {
        if (context.Blackboard == null) { return false; }

        Vector3 threatPos;
        if (_threatCategory != null
            && context.Blackboard.TryGet<AIPerceptionManager3D>(BBDataSig.PerceptionComp, out var perception)
            && perception != null)
        {
            var best = perception.GetBestMemoryForCategory(_threatCategory);
            if (best == null) { return false; }
            threatPos = best.LastKnownPosition;
        }
        else if (!context.Blackboard.TryGet<Vector3>(_threatPositionKey, out threatPos))
        {
            return false;
        }

        Vector3 fleeDir = ComputeFleeDirection(context.CurrentPosition, threatPos);
        if (fleeDir.IsZeroApprox()) { return false; }

        Rid map = nav.GetNavigationMap();

        foreach (float mult in FallbackMultipliers)
        {
            Vector3 projected = ProjectFleePoint(context.CurrentPosition, fleeDir, _fleeDistance * mult);
            Vector3 snapped = NavigationServer3D.MapGetClosestPoint(map, projected);

            if (snapped.DistanceTo(projected) > _maxSnapDistance) { continue; }

            var response = nav.RequestNewNavPath(snapped, overridePathCalcThresh: 0f);
            if (response == NavReqPathResponse.Success) { return true; }
        }

        JmoLogger.Warning(this, "ThreatAwareWaypoint: no reachable flee point at any fallback distance.");
        return false;
    }

    internal static Vector3 ComputeFleeDirection(Vector3 agentPos, Vector3 threatPos)
    {
        var toAgent = agentPos - threatPos;
        toAgent.Y = 0;
        return toAgent.LengthSquared() < 0.0001f ? Vector3.Zero : toAgent.Normalized();
    }

    internal static Vector3 ProjectFleePoint(Vector3 agentPos, Vector3 fleeDir, float distance)
    {
        return agentPos + fleeDir * distance;
    }

    #region Test Helpers
#if TOOLS
    internal float GetFleeDistance() => _fleeDistance;
    internal float GetMaxSnapDistance() => _maxSnapDistance;
    internal StringName GetThreatPositionKey() => _threatPositionKey;
    internal void SetFleeDistance(float value) => _fleeDistance = value;
    internal void SetMaxSnapDistance(float value) => _maxSnapDistance = value;
    internal void SetThreatPositionKey(StringName key) => _threatPositionKey = key;
    internal void SetThreatCategory(Category? category) => _threatCategory = category;
#endif
    #endregion
}
