namespace Jmodot.Implementation.Physics;

using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Physics;
using Jmodot.Implementation.AI.BB;

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
    /// <summary>Authored blackboard capability takes precedence over a body's fallback adapter.</summary>
    public static IImpactable? FindImpactable(Node node)
    {
        if (node.TryGetFirstChildOfInterface<IBlackboard>(out var bb) && bb != null
            && bb.TryGet<IImpactable>(BBDataSig.PhysicsInteraction, out var impactable) && impactable != null)
        {
            return impactable;
        }
        return node as IImpactable;
    }

    /// <summary>
    /// Resolves a participating target and applies its new velocity once per pair per physics frame.
    /// The caller applies the returned source velocity. A walker without an authored capability has
    /// its movement stability and the formula's default restitution; a non-participating source or target is skipped.
    /// Sustained-contact suppression belongs to the body-owned <see cref="ContinuingContactTracker"/>.
    /// </summary>
    public static ImpactSolveResult ResolveEntityContact(Node source, Node target, Vector3 incomingVelocity,
        Vector3 normal, float fallbackStability = 0f)
    {
        var self = FindImpactable(source);
        var other = FindImpactable(target);
        if (other == null || !other.ParticipatesInElasticCollisions
            || self is { ParticipatesInElasticCollisions: false }) { return ImpactSolveResult.None; }
        if (!ImpactFrameTracker.TryClaimPair(source.GetInstanceId(), target.GetInstanceId()))
        {
            return ImpactSolveResult.None;
        }
        var result = ResolveElasticCollision(incomingVelocity, other.Velocity,
            self?.Stability ?? fallbackStability, other.Stability, normal,
            CombineRestitution(self?.BounceRestitution ?? 0.8f, other.BounceRestitution));
        if (result.IsValid) { other.ApplyImpactVelocity(result.NewVelocityB); }
        return result;
    }

    /// <summary>
    /// Resolves an elastic collision between two entities.
    /// Uses mass derived from stability: mass = 1 + stability.
    /// Returns <see cref="ImpactSolveResult.None"/> when entities are separating.
    /// </summary>
    /// <param name="velocityA">Velocity of entity A (the resolving entity).</param>
    /// <param name="velocityB">Velocity of entity B (the target).</param>
    /// <param name="stabilityA">Stability of A (0 = light, higher = heavier).</param>
    /// <param name="stabilityB">Stability of B (float.MaxValue for immovable walls).</param>
    /// <param name="normal">Collision normal pointing from B toward A (Godot convention).</param>
    /// <param name="restitution">Pre-combined COR. 1.0 = elastic, 0 = inelastic.</param>
    public static ImpactSolveResult ResolveElasticCollision(
        Vector3 velocityA, Vector3 velocityB,
        float stabilityA, float stabilityB,
        Vector3 normal, float restitution = 0.8f)
    {
        // Closing speed: positive when approaching along normal axis
        float closingSpeed = (velocityA - velocityB).Dot(-normal);
        if (closingSpeed <= 0f)
        {
            return ImpactSolveResult.None;
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

        return new ImpactSolveResult(
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
