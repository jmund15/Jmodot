namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using Shared;

/// <summary>
/// Purposeful area search pattern. Maintains waypoint history — each new target
/// must be a minimum distance from the last N waypoints, creating systematic
/// area coverage instead of random revisiting.
///
/// 3-tier progressive fallback:
/// 1. Strict: reject candidates too close to any history point
/// 2. Relaxed: evict oldest history entry, retry
/// 3. Reset: clear all history, accept any valid nav point
/// </summary>
[GlobalClass, Tool]
public partial class HistoryAwareWaypointSelection : WaypointSelectionStrategy
{
    [Export(PropertyHint.Range, "2.0, 20.0, 0.5")]
    private float _minWaypointDistance = 5.0f;

    [Export(PropertyHint.Range, "2, 10, 1")]
    private int _maxHistorySize = 4;

    /// <summary>
    /// Checks whether a candidate waypoint is far enough from all points in the history.
    /// Distance is measured on the XZ plane (Y ignored). Uses squared distance to avoid sqrt.
    /// </summary>
    public static bool IsValidWaypoint(
        Vector3 candidate, IEnumerable<Vector3> history, float minDistance)
    {
        float minDistSq = minDistance * minDistance;
        foreach (var visited in history)
        {
            Vector3 offset = candidate - visited;
            offset.Y = 0;
            if (offset.LengthSquared() < minDistSq)
            {
                return false;
            }
        }
        return true;
    }

    public override bool TrySelectTarget(
        AINavigator3D nav, WaypointContext context,
        Queue<Vector3> waypointHistory)
    {
        // Tier 1: Strict — reject candidates too close to any history point
        if (TryPickValidTarget(nav, context, waypointHistory))
        {
            return true;
        }

        // Tier 2: Relaxed — evict oldest history entry, retry
        if (waypointHistory.Count > 0)
        {
            waypointHistory.Dequeue();
            JmoLogger.Info(this, "HistoryAwareWaypoint: relaxing history (evicted oldest) for target selection.");
            if (TryPickValidTarget(nav, context, waypointHistory))
            {
                return true;
            }
        }

        // Tier 3: Reset — clear all history, accept any reachable point
        waypointHistory.Clear();
        JmoLogger.Warning(this, "HistoryAwareWaypoint: cleared all history — falling back to any reachable point.");
        return TryPickAnyTarget(nav, context, waypointHistory);
    }

    private bool TryPickValidTarget(
        AINavigator3D nav, WaypointContext context,
        Queue<Vector3> waypointHistory)
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            Vector3 candidate = SampleCandidate(nav, context);
            if (!IsValidWaypoint(candidate, waypointHistory, _minWaypointDistance))
            {
                continue;
            }

            var response = nav.RequestNewNavPath(candidate, overridePathCalcThresh: 0f);
            if (response == NavReqPathResponse.Success)
            {
                AddToHistory(candidate, waypointHistory);
                return true;
            }
        }
        return false;
    }

    private bool TryPickAnyTarget(
        AINavigator3D nav, WaypointContext context,
        Queue<Vector3> waypointHistory)
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            Vector3 candidate = SampleCandidate(nav, context);
            var response = nav.RequestNewNavPath(candidate, overridePathCalcThresh: 0f);
            if (response == NavReqPathResponse.Success)
            {
                AddToHistory(candidate, waypointHistory);
                return true;
            }
        }

        JmoLogger.Warning(this, $"HistoryAwareWaypoint: failed to find any reachable target after {MaxAttempts} attempts.");
        return false;
    }

    private void AddToHistory(Vector3 waypoint, Queue<Vector3> waypointHistory)
    {
        waypointHistory.Enqueue(waypoint);
        while (waypointHistory.Count > _maxHistorySize)
        {
            waypointHistory.Dequeue();
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetMinWaypointDistance(float value) => _minWaypointDistance = value;
    internal void SetMaxHistorySize(int value) => _maxHistorySize = value;
    internal float GetMinWaypointDistance() => _minWaypointDistance;
    internal int GetMaxHistorySize() => _maxHistorySize;

    internal void AddToHistoryForTest(Vector3 wp, Queue<Vector3> history)
    {
        AddToHistory(wp, history);
    }
#endif
    #endregion
}
