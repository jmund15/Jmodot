namespace Jmodot.Implementation.Physics;

using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Physics;
using Jmodot.Implementation.AI.BB;
using Jmodot.Core.Stats;

/// <summary>
/// Elastic collision math and participating-entity contact dispatch.
/// Caller provides the pre-combined COR (coefficient of restitution):
///   - Entity-entity: use <see cref="CombineRestitution"/> (geometric mean).
///   - Surface bounce: use DurableCollisionResponse.VelocityRetention directly.
/// For wall bounces: pass stabilityB=float.MaxValue, velocityB=Zero — the formula
/// degenerates to simple reflection * COR (standard wall bounce).
/// </summary>
public static class ImpactPhysics
{
    /// <summary>The coefficient of restitution a contact uses when no participant authors one.</summary>
    public const float DefaultRestitution = 0.8f;

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
    /// A walker retains its post-slide velocity; other callers may apply the returned source velocity.
    /// A walker without an authored capability uses its movement stability and the default restitution.
    /// Non-participating targets return before source or stat lookups.
    /// A caller cancels its <see cref="ContinuingContactTracker"/> entry only for <see cref="ImpactSolveOutcome.Separating"/>:
    /// that contact can still close later, while a non-participant never resolves and a lost claim was resolved by its peer.
    /// </summary>
    public static ImpactSolveResult ResolveEntityContact(Node source, Node target, Vector3 incomingVelocity,
        Vector3 normal, IStatProvider? fallbackStats = null, Attribute? stabilityAttribute = null)
    {
        var other = FindImpactable(target);
        if (other == null || !other.ParticipatesInElasticCollisions) { return ImpactSolveResult.None; }
        var self = FindImpactable(source);
        if (self is { ParticipatesInElasticCollisions: false }) { return ImpactSolveResult.None; }
        if (!ImpactFrameTracker.TryClaimPair(source.GetInstanceId(), target.GetInstanceId()))
        {
            return ImpactSolveResult.None;
        }
        float stability = self?.Stability ?? (stabilityAttribute != null && fallbackStats != null
            ? fallbackStats.GetStatValue<float>(stabilityAttribute, 0f) : 0f);
        var result = ResolveElasticCollision(incomingVelocity, other.Velocity,
            stability, other.Stability, normal,
            CombineRestitution(self?.BounceRestitution ?? DefaultRestitution, other.BounceRestitution));
        if (result.IsValid) { other.ApplyImpactVelocity(result.NewVelocityB, source); }
        return result;
    }

    /// <summary>
    /// Relative speed of A toward B along the contact normal: positive when approaching, zero or negative when separating.
    /// </summary>
    /// <param name="normal">Collision normal pointing from B toward A (Godot convention).</param>
    public static float ClosingSpeed(Vector3 velocityA, Vector3 velocityB, Vector3 normal)
        => (velocityA - velocityB).Dot(-normal);

    /// <summary>
    /// Resolves an elastic collision between two entities.
    /// Uses mass derived from stability: mass = 1 + stability.
    /// Returns <see cref="ImpactSolveResult.Separating"/> when entities are not closing.
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
        Vector3 normal, float restitution = DefaultRestitution)
    {
        float closingSpeed = ClosingSpeed(velocityA, velocityB, normal);
        if (closingSpeed <= 0f)
        {
            return ImpactSolveResult.Separating;
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
