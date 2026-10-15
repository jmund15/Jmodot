namespace Jmodot.Implementation.Combat;

using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Implementation.Actors;

/// <summary>
/// Per-push shaping from the receiver. <paramref name="Damping"/> scales the incoming force before
/// stability; <paramref name="PopRatio"/> adds that share of the horizontal impulse to Y, capped at
/// <paramref name="PopMaxImpulse"/> (N·s, so the cap is a speed once mass divides).
/// </summary>
public readonly record struct KnockbackShaping(float Damping, float PopRatio, float PopMaxImpulse)
{
    public static KnockbackShaping None => new(1f, 0f, 0f);
}

/// <summary>
/// Pure resolution policy shared by every <see cref="IKnockbackReceiver3D" /> regime. Holds the
/// parts of knockback that do NOT vary by body type — stability scaling, the vertical-axis
/// decision, the applied-direction fallback and the audit-log write — so the next vertical-axis
/// rule is authored once. Only the mass step differs by regime and stays at the call site:
/// CharacterBody divides before applying, RigidBody divides only to report.
/// </summary>
public static class KnockbackPolicy
{

    /// <summary>
    /// The post-stability, post-vertical-decision impulse (N·s) and the direction the body
    /// actually received.
    /// </summary>
    public readonly record struct Resolved(Vector3 Impulse, Vector3 AppliedDirection);

    /// <param name="preserveVertical">
    /// The effective decision, already ORed from the receiver's own export and the producer's
    /// <see cref="KnockbackResult.PreserveVertical" />. False zeroes the impulse's Y.
    /// </param>
    public static Resolved Resolve(Vector3 direction, float incomingForce, float stability, bool preserveVertical)
        => Resolve(direction, incomingForce, stability, preserveVertical, KnockbackShaping.None);

    public static Resolved Resolve(Vector3 direction, float incomingForce, float stability, bool preserveVertical, in KnockbackShaping shaping)
    {
        var impulse = StabilityScaling.ScaleForce(direction * (incomingForce * shaping.Damping), stability);
        if (!preserveVertical)
        {
            impulse = new Vector3(impulse.X, 0f, impulse.Z);
        }

        if (shaping.PopRatio > 0f)
        {
            var pop = Mathf.Min(shaping.PopRatio * new Vector2(impulse.X, impulse.Z).Length(), shaping.PopMaxImpulse);
            if (pop > impulse.Y)
            {
                impulse = new Vector3(impulse.X, pop, impulse.Z);
            }
        }

        // Falling back to `direction` on a zero impulse matters: normalizing zero yields zero,
        // which reads as "no direction" rather than "no magnitude". Mass division is a positive
        // scalar, so the caller's post-division vector shares this direction.
        var appliedDirection = impulse.IsZeroApprox() ? direction : impulse.Normalized();
        return new Resolved(impulse, appliedDirection);
    }

    /// <summary>
    /// The force factor for a push <paramref name="secondsSinceLast"/> after the previous one:
    /// <paramref name="repeatFactor"/> strictly inside the window, 1 at or beyond it.
    /// </summary>
    public static float DampingFactor(double secondsSinceLast, float windowSeconds, float repeatFactor)
        => secondsSinceLast < windowSeconds ? repeatFactor : 1f;

    /// <summary>
    /// Audit-logs the applied knockback so HSM transition conditions (KnockbackCondition) can gate
    /// launch/stagger/ragdoll states. <paramref name="velocityDeltaMagnitude" /> is in m/s in every
    /// regime, so the two body types feed those conditions the same units. <paramref name="sustained" />
    /// carries the pushing hit's <see cref="CombatResult.Sustained" /> onto the log, so those conditions
    /// can skip a push from a held contact.
    /// </summary>
    public static void LogApplied(
        CombatLog? log,
        Node target,
        Node? attributedSource,
        Vector3 appliedDirection,
        float velocityDeltaMagnitude,
        bool sustained = false)
    {
        log?.Log(new KnockbackAppliedResult
        {
            Source = attributedSource,
            Target = target,
            Direction = appliedDirection,
            DeltaV = velocityDeltaMagnitude,
            Tags = System.Array.Empty<CombatTag>(),
            Sustained = sustained,
        });
    }
}
