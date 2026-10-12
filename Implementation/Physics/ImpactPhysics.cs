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
    /// A caller cancels its <see cref="ContinuingContactTracker"/> entry for <see cref="ImpactSolveOutcome.Separating"/> and
    /// <see cref="ImpactSolveOutcome.Inactive"/>: that contact can still resolve later, while a lost claim was resolved by its peer.
    /// </summary>
    public static ImpactSolveResult ResolveEntityContact(Node source, Node target, Vector3 incomingVelocity,
        Vector3 normal, IStatProvider? fallbackStats = null, Attribute? stabilityAttribute = null)
    {
        var other = FindImpactable(target);
        if (other == null) { return ImpactSolveResult.None; }
        if (!other.ParticipatesInElasticCollisions) { return ImpactSolveResult.Inactive; }
        var self = FindImpactable(source);
        if (self is { ParticipatesInElasticCollisions: false }) { return ImpactSolveResult.Inactive; }
        if (!ImpactFrameTracker.TryClaimPair(source.GetInstanceId(), target.GetInstanceId()))
        {
            return ImpactSolveResult.None;
        }
        float stability = self?.Stability ?? (stabilityAttribute != null && fallbackStats != null
            ? ClampStability(fallbackStats.GetStatValue<float>(stabilityAttribute, 0f)) : 0f);
        var result = ResolveElasticCollision(incomingVelocity, other.Velocity,
            stability, other.Stability, normal,
            CombineRestitution(self?.BounceRestitution ?? DefaultRestitution, other.BounceRestitution),
            ClampMass(self?.Mass ?? 1f), ClampMass(other.Mass));
        if (!result.IsValid) { return result; }
        result = result.WithNewVelocities(
            incomingVelocity + Lift(result.NewVelocityA - incomingVelocity, self?.LaunchLiftDegrees ?? 0f),
            other.Velocity + Lift(result.NewVelocityB - other.Velocity, other.LaunchLiftDegrees));
        other.ApplyImpactVelocity(result.NewVelocityB, source);
        return result;
    }

    /// <summary>
    /// Makes <paramref name="kick"/> climb at least <paramref name="degrees"/> above its horizontal heading: its
    /// horizontal part is kept and its vertical part becomes horizontal speed times the angle's tangent, unless the kick
    /// already climbs faster. A kick aimed into the ground (a contact above the body's centre) still leaves climbing.
    /// Takes a velocity change, so repeated kicks never lift speed the body already had. Angles outside 0..89 clamp; a
    /// non-finite angle lifts nothing.
    /// </summary>
    private static Vector3 Lift(Vector3 kick, float degrees)
    {
        if (!(degrees > 0f) || !float.IsFinite(degrees)) { return kick; }
        float speed = new Vector2(kick.X, kick.Z).Length();
        if (speed < Mathf.Epsilon) { return kick; }
        float angle = Mathf.DegToRad(Mathf.Min(degrees, 89f));
        return new Vector3(kick.X, Mathf.Max(kick.Y, speed * Mathf.Tan(angle)), kick.Z);
    }

    /// <summary>A mass the inertia formula can take: positive and finite, else 1.</summary>
    public static float ClampMass(float mass)
        => float.IsFinite(mass) && mass > 0f ? mass : 1f;

    /// <summary>A stability the inertia formula (Mass * (1 + Stability)) can take: NaN becomes 0 and negatives clamp to 0.</summary>
    public static float ClampStability(float stability)
        => float.IsNaN(stability) ? 0f : Mathf.Clamp(stability, 0f, float.MaxValue);

    /// <summary>
    /// Relative speed of A toward B along the contact normal: positive when approaching, zero or negative when separating.
    /// </summary>
    /// <param name="normal">Collision normal pointing from B toward A (Godot convention).</param>
    public static float ClosingSpeed(Vector3 velocityA, Vector3 velocityB, Vector3 normal)
        => (velocityA - velocityB).Dot(-normal);

    /// <summary>
    /// Resolves an elastic collision between two entities.
    /// Each body's inertia is mass * (1 + stability): stability resists, mass sets how much speed a push buys.
    /// Returns <see cref="ImpactSolveResult.Separating"/> when entities are not closing.
    /// </summary>
    /// <param name="velocityA">Velocity of entity A (the resolving entity).</param>
    /// <param name="velocityB">Velocity of entity B (the target).</param>
    /// <param name="stabilityA">Stability of A (0 = light, higher = heavier).</param>
    /// <param name="stabilityB">Stability of B (float.MaxValue for immovable walls).</param>
    /// <param name="normal">Collision normal pointing from B toward A (Godot convention).</param>
    /// <param name="restitution">Pre-combined COR. 1.0 = elastic, 0 = inelastic.</param>
    /// <param name="massA">Mass of A, positive and finite (<see cref="ClampMass"/>).</param>
    /// <param name="massB">Mass of B, positive and finite (<see cref="ClampMass"/>).</param>
    public static ImpactSolveResult ResolveElasticCollision(
        Vector3 velocityA, Vector3 velocityB,
        float stabilityA, float stabilityB,
        Vector3 normal, float restitution = DefaultRestitution,
        float massA = 1f, float massB = 1f)
    {
        float closingSpeed = ClosingSpeed(velocityA, velocityB, normal);
        if (closingSpeed <= 0f)
        {
            return ImpactSolveResult.Separating;
        }

        float inertiaA = massA * (1f + stabilityA);
        float inertiaB = massB * (1f + stabilityB);
        float totalInertia = inertiaA + inertiaB;

        // Use inertia ratios to avoid overflow with float.MaxValue stability (wall case)
        float ratioA = inertiaA / totalInertia;
        float ratioB = inertiaB / totalInertia;

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
