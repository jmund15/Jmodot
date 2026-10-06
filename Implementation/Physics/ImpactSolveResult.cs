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
    {
        NewVelocityA = default;
        NewVelocityB = default;
        ImpactForceOnA = 0f;
        ImpactForceOnB = 0f;
        Outcome = outcome;
    }

    /// <summary>Sentinel for a solve that never ran (non-participating body, pair already resolved this frame).</summary>
    public static ImpactSolveResult None => default;

    /// <summary>Sentinel for entities that were not closing (separating, or zero closing speed).</summary>
    public static ImpactSolveResult Separating => new(ImpactSolveOutcome.Separating);
}
