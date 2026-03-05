namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using Core.AI.Navigation.Zones;
using Shared;

/// <summary>
/// Default waypoint selection: picks random points inside a zone shape.
/// Uses WaypointContext.OriginPosition as the zone center.
/// Extracted from the original NavWanderAction's inline sampling logic.
/// </summary>
[GlobalClass, Tool]
public partial class ZoneWaypointSelection : WaypointSelectionStrategy
{
    [Export] private ZoneShape3D? _zone;

    [Export(PropertyHint.Range, "1, 20, 1")]
    private int _maxAttempts = 5;

    public override bool TrySelectTarget(
        AINavigator3D nav, WaypointContext context,
        Queue<Vector3> waypointHistory)
    {
        if (_zone == null)
        {
            JmoLogger.Warning(this, "ZoneWaypointSelection: no zone configured.");
            return false;
        }

        for (int i = 0; i < _maxAttempts; i++)
        {
            Vector3 candidate = _zone.SampleRandomInteriorPoint(context.OriginPosition);
            var response = nav.RequestNewNavPath(candidate, overridePathCalcThresh: 0f);
            if (response == NavReqPathResponse.Success)
            {
                return true;
            }
        }

        JmoLogger.Warning(this,
            $"ZoneWaypointSelection: failed to find reachable target after {_maxAttempts} attempts.");
        return false;
    }

    #region Test Helpers
#if TOOLS
    internal void SetZone(ZoneShape3D? zone) => _zone = zone;
    internal void SetMaxAttempts(int value) => _maxAttempts = value;
    internal ZoneShape3D? GetZone() => _zone;
    internal int GetMaxAttempts() => _maxAttempts;
#endif
    #endregion
}
