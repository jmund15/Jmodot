namespace Jmodot.Core.AI.Perception;

using BB;

/// <summary>Live primary-target memory reads without target selection or steering policy.</summary>
public static class BlackboardLiveTargetExts
{
    /// <summary>Returns a live memory and its flat last-known position; failures clear both outputs.</summary>
    public static bool TryGetLiveTarget(this IBlackboard bb, StringName key,
        out Perception3DInfo memory, out Vector3 flatPosition)
    {
        if (bb.TryGet<Perception3DInfo>(key, out var candidate) && candidate.IsLiveTarget())
        {
            memory = candidate;
            flatPosition = new Vector3(candidate.LastKnownPosition.X, 0, candidate.LastKnownPosition.Z);
            return true;
        }

        memory = null!;
        flatPosition = Vector3.Zero;
        return false;
    }
}
