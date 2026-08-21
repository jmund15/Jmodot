namespace Jmodot.Implementation.Movement.Strategies;

using AI.HSM;

/// <summary>
/// The walk-up check every claimant of the movement processor's single strategy-override slot shares.
/// </summary>
/// <remarks>
/// The slot is single-writer. A claimant nested under a <see cref="BTState"/> that also claims it will
/// stomp the state's override on enter and, worse, clear it outright on exit — silently reverting the
/// entity to default locomotion for the rest of that state. Nothing about that fails loudly at
/// runtime, so the check has to be an editor-visible configuration warning. It lives here rather than
/// on one action because the hazard belongs to the slot, not to whichever class noticed it first.
/// </remarks>
public static class MovementOverrideNesting
{
    /// <summary>
    /// The warning text for <paramref name="claimant"/> when an ancestor <see cref="BTState"/> already
    /// claims the strategy-override slot, or null when the nesting is safe. Call from
    /// <c>_GetConfigurationWarnings()</c>; a null claimant or one outside a tree returns null.
    /// </summary>
    public static string? DescribeConflict(Node? claimant)
    {
        for (var ancestor = claimant?.GetParent(); ancestor != null; ancestor = ancestor.GetParent())
        {
            if (ancestor is BTState { ClaimsMovementOverride: true } state)
            {
                return $"'{claimant!.Name}' sets a movement strategy override, but ancestor BTState "
                       + $"'{state.Name}' already claims that slot. The slot is single-writer: this node's "
                       + "exit would clear the state's override and revert the entity to default "
                       + "locomotion. Author one or the other, not both.";
            }
        }

        return null;
    }
}
