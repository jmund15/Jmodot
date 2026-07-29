namespace Jmodot.Core.Interaction;

/// <summary>
/// Why an attachment ended. The rider reads this to decide its own follow-up — only
/// <see cref="Shed"/> carries a fling impulse; every other cause is a plain release back to
/// normal AI.
/// </summary>
public enum DetachCause
{
    /// <summary>The host spent enough force to exhaust the rider's grip.</summary>
    Shed,

    /// <summary>The rider died while attached.</summary>
    RiderDied,

    /// <summary>The rider left the tree (freed, pooled, despawned) while attached.</summary>
    RiderRemoved,

    /// <summary>The host died, left the tree, or was freed while carrying the rider.</summary>
    HostRemoved,

    /// <summary>The host's capacity shrank below what it was already carrying.</summary>
    CapacityRevoked,
}
