namespace Jmodot.Implementation.Physics;

/// <summary>Why an elastic contact solve produced, or did not produce, new velocities.</summary>
public enum ImpactSolveOutcome
{
    /// <summary>No solve ran: the target has no impact capability, or its peer already resolved the pair this frame.</summary>
    Skipped,

    /// <summary>No solve ran because a body does not participate right now. It may participate later in the same contact.</summary>
    Inactive,

    /// <summary>The bodies were closing and the solve produced new velocities.</summary>
    Resolved,

    /// <summary>The bodies were not closing along the contact normal. The same contact can still close on a later step.</summary>
    Separating,
}
