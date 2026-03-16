namespace Jmodot.Implementation.AI.Navigation;

using Core.AI.BB;

/// <summary>
/// Contextual data passed from a waypoint action to its selection strategy.
/// Extensible: future strategies needing more data (velocity, BB) get new fields
/// without interface changes.
/// </summary>
public readonly struct WaypointContext
{
    /// <summary>
    /// Agent's position when the action entered — "home base."
    /// Zone strategies use this as zone center. Others may use as reference or ignore.
    /// </summary>
    public readonly Vector3 OriginPosition;

    /// <summary>
    /// Agent's current global position (for distance checks, nearby sampling).
    /// </summary>
    public readonly Vector3 CurrentPosition;

    /// <summary>
    /// Optional blackboard reference for strategies that need BB data (e.g., threat position).
    /// Null when the calling action doesn't provide one.
    /// </summary>
    public readonly IBlackboard? Blackboard;

    public WaypointContext(Vector3 originPosition, Vector3 currentPosition)
    {
        OriginPosition = originPosition;
        CurrentPosition = currentPosition;
        Blackboard = null;
    }

    public WaypointContext(Vector3 originPosition, Vector3 currentPosition, IBlackboard? blackboard)
    {
        OriginPosition = originPosition;
        CurrentPosition = currentPosition;
        Blackboard = blackboard;
    }
}
