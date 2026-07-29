namespace Jmodot.Core.Interaction;

/// <summary>
/// Selects which attached riders a shed's damage payload reaches. It selects the DAMAGE set only —
/// both values spend force identically (weakest remaining grip first, leftover discarded), so a
/// melee swing and a spell cast shed exactly the same riders for the same force and differ only in
/// who gets hurt.
/// </summary>
public enum ShedDamageScope
{
    /// <summary>Only riders the force actually shed take damage — the melee shape.</summary>
    ShedOnly,

    /// <summary>
    /// Every RIDING rider takes damage regardless of whether it was shed — the spell shape. Riders
    /// still flying to a reservation are outside every shed's reach, damage included.
    /// </summary>
    AllAttached,
}
