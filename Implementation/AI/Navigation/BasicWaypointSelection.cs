namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using Shared;

/// <summary>
/// Simplest waypoint selection: sample a candidate point and request a nav path.
/// With a zone configured, candidates come from zone interior sampling.
/// Without a zone, candidates come from random nav mesh points.
/// </summary>
[GlobalClass, Tool]
public partial class BasicWaypointSelection : WaypointSelectionStrategy
{
    public override bool TrySelectTarget(
        AINavigator3D nav, WaypointContext context,
        Queue<Vector3> waypointHistory)
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            Vector3 candidate = SampleCandidate(nav, context);
            var response = nav.RequestNewNavPath(candidate, overridePathCalcThresh: 0f);
            if (response == NavReqPathResponse.Success)
            {
                return true;
            }
        }

        JmoLogger.Warning(this,
            $"BasicWaypointSelection: failed to find reachable target after {MaxAttempts} attempts.");
        return false;
    }
}
