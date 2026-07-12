namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using Core.AI.Navigation.Zones;
using Shared;

/// <summary>
/// Controls what position the zone is centered on when sampling candidates.
/// Origin: zone center = agent's position when the action first entered ("home base").
/// Current: zone center = agent's current position ("roaming search").
/// </summary>
public enum ZoneCenterMode { Origin, Current }

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
    /// Controls what the zone is centered on. Origin = spawn position (home base).
    /// Current = agent's live position (roaming search — zone follows the agent).
    /// </summary>
    [Export] private ZoneCenterMode _zoneCenterMode = ZoneCenterMode.Origin;

    /// <summary>
    /// Shared retry budget for candidate sampling loops.
    /// </summary>
    [Export(PropertyHint.Range, "1, 25, 1")]
    private int _maxAttempts = 10;

    /// <summary>
    /// Optional BB key holding a Vector4 dynamic zone (XYZ = center, W = radius). When set and the
    /// key resolves with W &gt; 0, candidate sampling uses this disc INSTEAD of the exported
    /// zone/fallback — lets a strategy honor a runtime zone (e.g. room leash) without hard-coding
    /// game keys.
    /// </summary>
    [Export] private StringName _zoneBlackboardKey = new("");

    /// <summary>
    /// Margin scale applied to W (room-leash wander uses 0.85 so waypoints don't hug the leash boundary).
    /// </summary>
    [Export(PropertyHint.Range, "0.1,2.0,0.05")] private float _bbZoneRadiusScale = 1.0f;

    protected ZoneShape3D? Zone => _zone;
    protected int MaxAttempts => _maxAttempts;

    private bool _warnedNoRng;

    /// <summary>
    /// Samples a candidate waypoint. A runtime BB zone (when configured and live) wins; otherwise
    /// delegates to the exported zone if configured, else falls back to a random nav mesh point.
    /// </summary>
    protected Vector3 SampleCandidate(AINavigator3D nav, WaypointContext context)
    {
        if (TrySampleBlackboardZone(nav, context, out var bbCandidate))
        {
            return bbCandidate;
        }

        if (_zone != null)
        {
            var center = _zoneCenterMode == ZoneCenterMode.Current
                ? context.CurrentPosition
                : context.OriginPosition;
            center = nav.SnapToNavMesh(center);
            return _zone.SampleRandomInteriorPoint(center, ResolveRng(context));
        }
        return nav.SampleRandomNavPoint();
    }

    // Runtime BB zone (XYZ center, W radius) overrides the exported zone when present and W > 0.
    // Uniform-area disc sampling on the horizontal plane (r = radius * sqrt(u)); snapped so off-mesh
    // samples never hit the navigator's unreachable-warning path.
    private bool TrySampleBlackboardZone(AINavigator3D nav, WaypointContext context, out Vector3 candidate)
    {
        candidate = Vector3.Zero;
        if (_zoneBlackboardKey.IsEmpty) { return false; }

        var bb = context.Blackboard;
        if (bb == null) { return false; }
        if (!bb.TryGet<Vector4>(_zoneBlackboardKey, out var zone) || zone.W <= 0f) { return false; }

        var rng = ResolveRng(context);
        var center = new Vector3(zone.X, zone.Y, zone.Z);
        var radius = zone.W * _bbZoneRadiusScale;
        var angle = rng.GetRndFloat() * Mathf.Tau;
        var distance = radius * Mathf.Sqrt(rng.GetRndFloat());
        var point = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        candidate = nav.SnapToNavMesh(point);
        return true;
    }

    private JmoRng ResolveRng(WaypointContext context)
    {
        var rng = context.Rng;
        if (rng != null) { return rng; }
        if (!_warnedNoRng)
        {
            JmoLogger.Warning(this, "[Lineage] WaypointSelectionStrategy: no per-agent Rng in context — UnseededByDesign fallback (non-deterministic sampling).");
            _warnedNoRng = true;
        }
        return JmoRng.UnseededByDesign();
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
    internal void SetZoneCenterMode(ZoneCenterMode mode) => _zoneCenterMode = mode;
    internal void SetZoneBlackboardKey(StringName key) => _zoneBlackboardKey = key;
    internal void SetBbZoneRadiusScale(float value) => _bbZoneRadiusScale = value;
    internal ZoneShape3D? GetZone() => _zone;
    internal int GetMaxAttempts() => _maxAttempts;
    internal ZoneCenterMode GetZoneCenterMode() => _zoneCenterMode;
    internal Vector3 SampleCandidateForTest(AINavigator3D nav, WaypointContext context)
        => SampleCandidate(nav, context);
#endif
    #endregion
}
