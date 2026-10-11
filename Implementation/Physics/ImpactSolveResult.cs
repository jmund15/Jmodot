namespace Jmodot.Implementation.Physics;

using Godot;

/// <summary>
/// Result of an elastic collision resolution between two entities.
/// Contains post-collision velocities and impact force magnitudes for both participants.
/// <see cref="Outcome"/> says why a result carries no velocities: <see cref="None"/> for a solve that never ran,
/// <see cref="Separating"/> for entities that were not closing.
/// </summary>
public readonly struct ImpactSolveResult
{
    public Vector3 NewVelocityA { get; }
    public Vector3 NewVelocityB { get; }

    /// <summary>Magnitude of impulse applied to A. Useful for VFX/audio/stagger scaling.</summary>
    public float ImpactForceOnA { get; }

    /// <summary>Magnitude of impulse applied to B. Useful for VFX/audio/stagger scaling.</summary>
    public float ImpactForceOnB { get; }

    public ImpactSolveOutcome Outcome { get; }

    /// <summary>True only when the entities were closing and the solve produced velocities.</summary>
    public bool IsValid => Outcome == ImpactSolveOutcome.Resolved;

    public ImpactSolveResult(
        Vector3 newVelocityA, Vector3 newVelocityB,
        float impactForceOnA, float impactForceOnB)
    {
        NewVelocityA = newVelocityA;
        NewVelocityB = newVelocityB;
        ImpactForceOnA = impactForceOnA;
        ImpactForceOnB = impactForceOnB;
        Outcome = ImpactSolveOutcome.Resolved;
    }

    private ImpactSolveResult(ImpactSolveOutcome outcome)
        : this(default, default, 0f, 0f, outcome) { }

    private ImpactSolveResult(
        Vector3 newVelocityA, Vector3 newVelocityB,
        float impactForceOnA, float impactForceOnB, ImpactSolveOutcome outcome)
    {
        NewVelocityA = newVelocityA;
        NewVelocityB = newVelocityB;
        ImpactForceOnA = impactForceOnA;
        ImpactForceOnB = impactForceOnB;
        Outcome = outcome;
    }

    /// <summary>This result with <see cref="NewVelocityB"/> replaced; the outcome and forces are unchanged.</summary>
    public ImpactSolveResult WithNewVelocityB(Vector3 newVelocityB)
        => new(NewVelocityA, newVelocityB, ImpactForceOnA, ImpactForceOnB, Outcome);

    /// <summary>Sentinel for a solve that never ran (no impact capability, pair already resolved this frame).</summary>
    public static ImpactSolveResult None => default;

    /// <summary>Sentinel for a body that does not participate right now.</summary>
    public static ImpactSolveResult Inactive => new(ImpactSolveOutcome.Inactive);

    /// <summary>Sentinel for entities that were not closing (separating, or zero closing speed).</summary>
    public static ImpactSolveResult Separating => new(ImpactSolveOutcome.Separating);
}
