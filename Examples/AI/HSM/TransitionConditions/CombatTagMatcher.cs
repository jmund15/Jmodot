namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Combat;

/// <summary>
/// Static helper for matching CombatTags between result tags and required tags.
/// Eliminates duplication across DamageCondition, KnockbackCondition, etc.
/// </summary>
public static class CombatTagMatcher
{
    /// <summary>
    /// Checks if the result tags match the required tags based on the specified mode.
    /// </summary>
    /// <param name="resultTags">Tags present on the combat result.</param>
    /// <param name="requiredTags">Tags required to match.</param>
    /// <param name="mode">How to match: Any (OR) or All (AND).</param>
    /// <returns>True if tags match according to the mode.</returns>
    public static bool MatchesTags(
        IEnumerable<CombatTag>? resultTags,
        IEnumerable<CombatTag>? requiredTags,
        TagMatchMode mode)
    {
        if (requiredTags == null || !requiredTags.Any())
        {
            return true;
        }

        if (resultTags == null)
        {
            return false;
        }

        return mode switch
        {
            TagMatchMode.Any => requiredTags.Any(req => resultTags.Any(res => res == req)),
            TagMatchMode.All => requiredTags.All(req => resultTags.Any(res => res == req)),
            _ => false
        };
    }
}
