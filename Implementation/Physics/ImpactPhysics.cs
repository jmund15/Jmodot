namespace Jmodot.Implementation.Physics;

using Godot;

/// <summary>
/// Pure static utility for elastic collision resolution between two entities.
/// Caller provides the pre-combined COR (coefficient of restitution):
///   - Entity-entity: use <see cref="CombineRestitution"/> (geometric mean).
///   - Surface bounce: use DurableCollisionResponse.VelocityRetention directly.
/// For wall bounces: pass stabilityB=float.MaxValue, velocityB=Zero — the formula
/// degenerates to simple reflection * COR (standard wall bounce).
/// </summary>
public static class ImpactPhysics
{
    /// <summary>
    /// Resolves an elastic collision between two entities.
    /// Uses mass derived from stability: mass = 1 + stability.
    /// Returns <see cref="ImpactResult.None"/> when entities are separating.
    /// </summary>
    /// <param name="velocityA">Velocity of entity A (the resolving entity).</param>
    /// <param name="velocityB">Velocity of entity B (the target).</param>
    /// <param name="stabilityA">Stability of A (0 = light, higher = heavier).</param>
    /// <param name="stabilityB">Stability of B (float.MaxValue for immovable walls).</param>
    /// <param name="normal">Collision normal pointing from B toward A (Godot convention).</param>
    /// <param name="restitution">Pre-combined COR. 1.0 = elastic, 0 = inelastic.</param>
    public static ImpactResult ResolveElasticCollision(
        Vector3 velocityA, Vector3 velocityB,
        float stabilityA, float stabilityB,
        Vector3 normal, float restitution = 0.8f)
    {
        // Closing speed: positive when approaching along normal axis
        float closingSpeed = (velocityA - velocityB).Dot(-normal);
        if (closingSpeed <= 0f)
        {
            return ImpactResult.None;
        }

        // Mass from stability: stability=0 → mass=1, stability=3 → mass=4
        float massA = 1f + stabilityA;
        float massB = 1f + stabilityB;
        float totalMass = massA + massB;

        // Use mass ratios to avoid overflow with float.MaxValue stability (wall case)
        float ratioA = massA / totalMass;
        float ratioB = massB / totalMass;

        float scaledClosing = (1f + restitution) * closingSpeed;

        Vector3 impulseOnA = normal * (scaledClosing * ratioB);
        Vector3 impulseOnB = -normal * (scaledClosing * ratioA);

        return new ImpactResult(
            newVelocityA: velocityA + impulseOnA,
            newVelocityB: velocityB + impulseOnB,
            impactForceOnA: impulseOnA.Length(),
            impactForceOnB: impulseOnB.Length());
    }

    /// <summary>
    /// Combines two entity COR values via geometric mean: sqrt(a * b).
    /// Ensures both entities' bounciness contributes to the combined restitution.
    /// </summary>
    public static float CombineRestitution(float a, float b)
    {
        return Mathf.Sqrt(a * b);
    }
}
