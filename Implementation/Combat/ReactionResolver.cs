using System.Linq;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat;

/// <summary>
/// Helper class to resolve the primary reaction from a payload.
/// </summary>
public static class ReactionResolver
{
    /// <summary>
    /// Returns the GameplayTag with the highest priority from all effects in the payload.
    /// Returns null if no tags are found.
    /// </summary>
    public static CombatTag GetHighestPriorityTag(IAttackPayload payload)
    {
        if (payload == null || payload.Effects == null || payload.Effects.Count == 0)
        {
            return null;
        }

        CombatTag highestPriorityTag = null;
        int maxPriority = int.MinValue;

        foreach (var effect in payload.Effects)
        {
            if (effect.Tags == null) { continue; }

            foreach (var tag in effect.Tags)
            {
                if (tag == null) { continue; }

                if (tag.Priority > maxPriority)
                {
                    maxPriority = tag.Priority;
                    highestPriorityTag = tag;
                }
            }
        }

        return highestPriorityTag;
    }
}
