namespace Jmodot.Implementation.Physics;

using Godot;

/// <summary>
/// Result of an elastic collision resolution between two entities.
/// Contains post-collision velocities and impact force magnitudes for both participants.
/// Use <see cref="None"/> when entities are separating (no collision to resolve).
/// </summary>
public readonly struct ImpactResult
{
    public Vector3 NewVelocityA { get; }
    public Vector3 NewVelocityB { get; }

    /// <summary>Magnitude of impulse applied to A. Useful for VFX/audio/stagger scaling.</summary>
    public float ImpactForceOnA { get; }

    /// <summary>Magnitude of impulse applied to B. Useful for VFX/audio/stagger scaling.</summary>
    public float ImpactForceOnB { get; }

    /// <summary>False when entities are separating or collision was invalid.</summary>
    public bool IsValid { get; }

    public ImpactResult(
        Vector3 newVelocityA, Vector3 newVelocityB,
        float impactForceOnA, float impactForceOnB)
    {
        NewVelocityA = newVelocityA;
        NewVelocityB = newVelocityB;
        ImpactForceOnA = impactForceOnA;
        ImpactForceOnB = impactForceOnB;
        IsValid = true;
    }

    /// <summary>Sentinel for non-collisions (separating entities, zero closing speed).</summary>
    public static ImpactResult None => default;
}
