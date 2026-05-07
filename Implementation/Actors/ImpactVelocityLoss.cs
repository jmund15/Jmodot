namespace Jmodot.Implementation.Actors;

using Godot;

/// <summary>
/// Pure-math helper: computes the velocity an entity retains after losing kinetic energy
/// proportional to the damage it dealt during a collision. Used by
/// <see cref="ForceImpactDamageApplier"/> to slow launched entities on impact (Wind-Blast
/// chain mechanic — entities lose more velocity into denser targets, less into
/// glancing/low-absorption ones).
/// </summary>
public static class ImpactVelocityLoss
{
    /// <summary>
    /// Returns the velocity remaining after this entity dealt <paramref name="damage"/> into a
    /// target with the given <paramref name="absorption"/> (0..1, clamped) while moving at
    /// <paramref name="currentVelocity"/> with the given <paramref name="mass"/>.
    /// </summary>
    /// <remarks>
    /// Direction is preserved; only magnitude reduces. The full-stop boundary is inclusive — if
    /// the computed loss would overshoot into reverse motion, the result is <see cref="Vector3.Zero"/>
    /// (no rebound). Degenerate inputs (mass &lt;= 0, |velocity| == 0) return safely without throwing;
    /// callers should log warnings on degenerate mass since they own the runtime context.
    /// </remarks>
    public static Vector3 ComputeNewVelocity(
        Vector3 currentVelocity,
        float damage,
        float absorption,
        float mass)
    {
        if (!float.IsFinite(damage) || !float.IsFinite(absorption) || !float.IsFinite(mass))
        {
            return currentVelocity;
        }

        if (mass <= 0f)
        {
            return currentVelocity;
        }

        var currentMagnitude = currentVelocity.Length();
        if (currentMagnitude <= 0f)
        {
            return Vector3.Zero;
        }

        var clampedAbsorption = Mathf.Clamp(absorption, 0f, 1f);
        var energySpent = damage * clampedAbsorption;
        var velocityLoss = energySpent / mass;

        if (velocityLoss >= currentMagnitude)
        {
            return Vector3.Zero;
        }

        return currentVelocity - currentVelocity.Normalized() * velocityLoss;
    }
}
