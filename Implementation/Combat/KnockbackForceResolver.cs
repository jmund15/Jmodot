namespace Jmodot.Implementation.Combat;

using Godot;
using Jmodot.Core.Combat;

/// <summary>
/// Pure-static force resolver shared by knockback-emitting effects.
/// Computes the final designer-tuned force from a base value, optional spatial
/// modulation curves (distance falloff + cone-angle falloff), and a velocity scaling
/// term. Belongs to the Logic Domain — no Node, no scene context, no side effects.
/// </summary>
/// <remarks>
/// Formula: <c>baseForce × spatial × velocityMult</c>
/// where:
///   <c>spatial = distanceFalloff.Sample(distance/dNorm) × angleFalloff.Sample(angle/aNorm)</c>
///   <c>velocityMult = 1 + (impactVelocity.Length × velocityScaling × 0.1)</c>
/// Either curve null collapses its factor to 1 (no modulation).
///
/// Angle convention: <c>(-context.HitDirection).AngleTo(context.EpicenterForward)</c>.
/// For an on-axis hit (target directly ahead of attacker), <c>HitDirection</c> points
/// toward the target and <c>EpicenterForward</c> is the source's <c>Basis.Z</c> — these
/// align to 0°. As targets drift off-axis, the angle grows toward the cone half-angle.
/// </remarks>
public static class KnockbackForceResolver
{
    public static float Resolve(
        float baseForce,
        Curve? distanceFalloff,
        float distanceNormalizer,
        Curve? angleFalloff,
        float angleNormalizerDegrees,
        HitContext context,
        float velocityScaling)
    {
        var dFactor = 1f;
        if (distanceFalloff is not null && distanceNormalizer > 0f)
        {
            var dNorm = Mathf.Clamp(context.DistanceFromEpicenter / distanceNormalizer, 0f, 1f);
            dFactor = distanceFalloff.Sample(dNorm);
        }

        var aFactor = 1f;
        if (angleFalloff is not null && angleNormalizerDegrees > 0f)
        {
            var angleRad = (-context.HitDirection).AngleTo(context.EpicenterForward);
            var aNorm = Mathf.Clamp(Mathf.RadToDeg(angleRad) / angleNormalizerDegrees, 0f, 1f);
            aFactor = angleFalloff.Sample(aNorm);
        }

        var spatial = dFactor * aFactor;
        var velMult = 1f + (context.ImpactVelocity.Length() * velocityScaling * 0.1f);
        return baseForce * spatial * velMult;
    }
}
