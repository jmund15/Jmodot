namespace Jmodot.Core.AI.Perception;

using BB;

/// <summary>Live primary-target memory reads without target selection or steering policy.</summary>
public static class BlackboardLiveTargetExts
{
    /// <summary>Returns a live memory; a failure clears the output.</summary>
    public static bool TryGetLiveTarget(this IBlackboard bb, StringName key, out Perception3DInfo memory)
    {
        if (bb.TryGet<Perception3DInfo>(key, out var candidate) && candidate.IsLiveTarget())
        {
            memory = candidate;
            return true;
        }

        memory = null!;
        return false;
    }
}
