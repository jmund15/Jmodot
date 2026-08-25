namespace Jmodot.Implementation.Combat;

using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Implementation.Actors;

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
    {
        var impulse = StabilityScaling.ScaleForce(direction * incomingForce, stability);
        if (!preserveVertical)
        {
            impulse = new Vector3(impulse.X, 0f, impulse.Z);
        }

        // Falling back to `direction` on a zero impulse matters: normalizing zero yields zero,
        // which reads as "no direction" rather than "no magnitude". Mass division is a positive
        // scalar, so the caller's post-division vector shares this direction.
        var appliedDirection = impulse.IsZeroApprox() ? direction : impulse.Normalized();
        return new Resolved(impulse, appliedDirection);
    }

    /// <summary>
    /// Audit-logs the applied knockback so HSM transition conditions (KnockbackCondition) can gate
    /// launch/stagger/ragdoll states. <paramref name="velocityDeltaMagnitude" /> is in m/s in every
    /// regime, so the two body types feed those conditions the same units.
    /// </summary>
    public static void LogApplied(
        CombatLog? log,
        Node target,
        Node? attributedSource,
        Vector3 appliedDirection,
        float velocityDeltaMagnitude)
    {
        log?.Log(new KnockbackResult
        {
            Source = attributedSource,
            Target = target,
            Direction = appliedDirection,
            Force = velocityDeltaMagnitude,
            Tags = System.Array.Empty<CombatTag>()
        });
    }
}
