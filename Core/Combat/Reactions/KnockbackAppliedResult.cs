namespace Jmodot.Core.Combat.Reactions;

/// <summary>
/// The push a knockback receiver actually applied, logged by the receiver after stability, the
/// vertical decision and mass. <see cref="DeltaV"/> is a velocity change in m/s, where
/// <see cref="KnockbackResult.Force"/> is the producer's impulse in N·s; deliberately not an
/// <see cref="IForceCarrier"/>, so no receiver can apply it again.
/// </summary>
public record KnockbackAppliedResult : CombatResult
{
    /// <summary>Unit vector of the velocity change the body actually received.</summary>
    public Vector3 Direction { get; init; }

    /// <summary>Magnitude of the velocity change in m/s.</summary>
    public float DeltaV { get; init; }
}
