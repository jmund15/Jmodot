namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using System.Linq;
using Core.Identification;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.AI.Perception;
using Physics;
using Shared;

/// <summary>
/// Waypoint selection strategy that finds a cover position behind a nearby obstacle,
/// sheltered from a threat position stored on the blackboard. Evaluates obstacles via
/// physics sphere query, computes far-side surface points via ShapeProximityCalculator,
/// verifies line-of-sight occlusion via raycast, and scores candidates by proximity
/// and safety. Falls back to a flee projection if no cover is found.
///
/// Designed for shelter/hide behaviors where the critter seeks a static safe position.
/// Pairs with WaypointSteeringAction (_succeedOnReach=true) in a BT Sequence.
/// </summary>
[GlobalClass, Tool]
public partial class CoverWaypointSelection : WaypointSelectionStrategy
{
    /// <summary>
    /// Category to query from AIPerceptionManager3D for threat position.
    /// When set, queries perception directly. When null, falls back to BB key.
    /// </summary>
    [Export] private Category? _threatCategory;

    /// <summary>Legacy BB key fallback. Only used when _threatCategory is null.</summary>
    [Export] private StringName _threatPositionKey = new("Critter_ThreatPosition");

    [Export(PropertyHint.Range, "5.0, 25.0, 0.5")]
    private float _searchRadius = 12.0f;

    [Export(PropertyHint.Range, "0.5, 3.0, 0.1")]
    private float _coverOffset = 1.5f;

    [Export(PropertyHint.Layers3DPhysics)]
    private uint _obstacleMask = 64;

    [Export(PropertyHint.Range, "1.0, 10.0, 0.5")]
    private float _maxSnapDistance = 3.0f;

    [Export(PropertyHint.Range, "1, 12, 1")]
    private int _maxObstacles = 8;

    [Export(PropertyHint.Range, "3.0, 20.0, 0.5")]
    private float _fallbackFleeDistance = 8.0f;

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

        Vector3 agentPos = context.CurrentPosition;
        Vector3 toThreat = agentPos - threatPos;
        toThreat.Y = 0;
        if (toThreat.LengthSquared() < 0.0001f) { return false; }

        var obstacles = FindNearbyObstacles(nav, agentPos);
        if (obstacles.Count > 0)
        {
            if (TryFindCoverBehindObstacle(nav, agentPos, threatPos, obstacles))
            { return true; }
        }

        return TryFallbackFlee(nav, agentPos, threatPos);
    }

    private List<Node3D> FindNearbyObstacles(AINavigator3D nav, Vector3 agentPos)
    {
        var spaceState = (nav.GetParent() as Node3D)?.GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) { return new List<Node3D>(); }

        var sphereShape = new SphereShape3D();
        sphereShape.Radius = _searchRadius;

        var query = new PhysicsShapeQueryParameters3D();
        query.Shape = sphereShape;
        query.Transform = new Transform3D(Basis.Identity, agentPos);
        query.CollisionMask = _obstacleMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var results = spaceState.IntersectShape(query, _maxObstacles * 2);

        var bodies = new List<(Node3D body, float distSq)>();
        foreach (var result in results)
        {
            if (!result.ContainsKey("collider")) { continue; }
            var colliderObj = result["collider"].As<Node3D>();
            if (colliderObj == null) { continue; }

            float distSq = agentPos.DistanceSquaredTo(colliderObj.GlobalPosition);
            bodies.Add((colliderObj, distSq));
        }

        return bodies
            .OrderBy(b => b.distSq)
            .Take(_maxObstacles)
            .Select(b => b.body)
            .ToList();
    }

    private bool TryFindCoverBehindObstacle(
        AINavigator3D nav, Vector3 agentPos, Vector3 threatPos, List<Node3D> obstacles)
    {
        Rid map = nav.GetNavigationMap();
        var spaceState = (nav.GetParent() as Node3D)?.GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) { return false; }

        float bestScore = 0f;
        Vector3 bestCandidate = Vector3.Zero;

        foreach (var obstacle in obstacles)
        {
            if (!GodotObject.IsInstanceValid(obstacle)) { continue; }

            Vector3 obstacleCenter = obstacle.GlobalPosition;
            obstacleCenter.Y = 0;

            Vector3 probePoint = ComputeFarSideProbe(obstacleCenter, threatPos, _searchRadius * 0.5f);
            Vector3 farSideSurface = ShapeProximityCalculator.GetClosestSurfacePointOnBody(probePoint, obstacle);
            Vector3 threatDir = obstacleCenter - threatPos;
            threatDir.Y = 0;
            if (threatDir.LengthSquared() < 0.0001f) { continue; }
            threatDir = threatDir.Normalized();

            Vector3 candidate = ComputeCoverPosition(farSideSurface, threatDir, _coverOffset, agentPos.Y);
            Vector3 snapped = NavigationServer3D.MapGetClosestPoint(map, candidate);

            if (snapped.DistanceTo(candidate) > _maxSnapDistance) { continue; }

            bool losBlocked = IsLOSBlocked(spaceState, threatPos, snapped);
            float score = ScoreCoverCandidate(snapped, agentPos, threatPos, losBlocked, _searchRadius);

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = snapped;
            }
        }

        if (bestScore <= 0f) { return false; }

        var response = nav.RequestNewNavPath(bestCandidate, overridePathCalcThresh: 0f);
        if (response == NavReqPathResponse.Success)
        {
            JmoLogger.Info(this, $"Cover waypoint selected (score={bestScore:F2}).");
            return true;
        }

        return false;
    }

    private bool IsLOSBlocked(PhysicsDirectSpaceState3D spaceState, Vector3 from, Vector3 to)
    {
        var rayQuery = PhysicsRayQueryParameters3D.Create(
            new Vector3(from.X, 1f, from.Z),
            new Vector3(to.X, 1f, to.Z),
            _obstacleMask);
        rayQuery.CollideWithAreas = false;
        rayQuery.CollideWithBodies = true;

        var hit = spaceState.IntersectRay(rayQuery);
        return hit.Count > 0;
    }

    private bool TryFallbackFlee(AINavigator3D nav, Vector3 agentPos, Vector3 threatPos)
    {
        Vector3 fleeDir = ThreatAwareWaypointSelection.ComputeFleeDirection(agentPos, threatPos);
        if (fleeDir.IsZeroApprox()) { return false; }

        Rid map = nav.GetNavigationMap();

        foreach (float mult in FallbackMultipliers)
        {
            Vector3 projected = ThreatAwareWaypointSelection.ProjectFleePoint(
                agentPos, fleeDir, _fallbackFleeDistance * mult);
            Vector3 snapped = NavigationServer3D.MapGetClosestPoint(map, projected);

            if (snapped.DistanceTo(projected) > _maxSnapDistance) { continue; }

            var response = nav.RequestNewNavPath(snapped, overridePathCalcThresh: 0f);
            if (response == NavReqPathResponse.Success)
            {
                JmoLogger.Warning(this, "No cover found — falling back to flee projection.");
                return true;
            }
        }

        JmoLogger.Warning(this, "Cover + flee fallback both failed.");
        return false;
    }

    internal static Vector3 ComputeCoverPosition(
        Vector3 farSideSurface, Vector3 threatDir, float coverOffset, float agentY)
    {
        var pos = farSideSurface + threatDir * coverOffset;
        pos.Y = agentY;
        return pos;
    }

    internal static float ScoreCoverCandidate(
        Vector3 candidate, Vector3 agentPos, Vector3 threatPos,
        bool losBlocked, float searchRadius)
    {
        if (!losBlocked) { return 0f; }

        float distToAgent = FlatDistance(candidate, agentPos);
        float distToThreat = FlatDistance(candidate, threatPos);

        float proximityScore = 1f - Mathf.Clamp(distToAgent / searchRadius, 0f, 1f);
        float safetyScore = Mathf.Clamp(distToThreat / searchRadius, 0f, 1f);

        return proximityScore * 0.6f + safetyScore * 0.4f;
    }

    internal static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    internal static Vector3 ComputeFarSideProbe(
        Vector3 obstacleCenter, Vector3 threatPos, float probeDistance)
    {
        var throughDir = obstacleCenter - threatPos;
        throughDir.Y = 0;
        if (throughDir.LengthSquared() < 0.0001f) { return obstacleCenter; }
        throughDir = throughDir.Normalized();
        return obstacleCenter + throughDir * probeDistance;
    }

    #region Test Helpers
#if TOOLS
    internal float GetSearchRadius() => _searchRadius;
    internal void SetSearchRadius(float value) => _searchRadius = value;
    internal float GetCoverOffset() => _coverOffset;
    internal void SetCoverOffset(float value) => _coverOffset = value;
    internal uint GetObstacleMask() => _obstacleMask;
    internal void SetObstacleMask(uint value) => _obstacleMask = value;
    internal float GetMaxSnapDistance() => _maxSnapDistance;
    internal void SetMaxSnapDistance(float value) => _maxSnapDistance = value;
    internal int GetMaxObstacles() => _maxObstacles;
    internal void SetMaxObstacles(int value) => _maxObstacles = value;
    internal float GetFallbackFleeDistance() => _fallbackFleeDistance;
    internal void SetFallbackFleeDistance(float value) => _fallbackFleeDistance = value;
    internal StringName GetThreatPositionKey() => _threatPositionKey;
    internal void SetThreatPositionKey(StringName key) => _threatPositionKey = key;
    internal void SetThreatCategory(Category? category) => _threatCategory = category;
#endif
    #endregion
}
