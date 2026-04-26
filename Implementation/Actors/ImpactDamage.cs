namespace Jmodot.Implementation.Actors;

/// <summary>
/// Pure static calculator for velocity-based impact damage.
/// Damage scales linearly with velocity magnitude at impact.
/// Surface kind (wall, floor, ceiling, entity) is irrelevant to the formula —
/// classification is the consumer's job (via <see cref="ImpactInfo"/> helpers
/// or the Category system).
/// </summary>
public static class ImpactDamage
{
    /// <summary>
    /// Calculates damage from an impact based on velocity at time of collision.
    /// </summary>
    /// <param name="velocityMagnitude">Speed at impact (m/s). Negative values treated as zero.</param>
    /// <param name="damageMultiplier">Scaling factor for velocity-to-damage conversion.</param>
    /// <returns>Damage amount (always &gt;= 0).</returns>
    public static float CalculateFromSpeed(float velocityMagnitude, float damageMultiplier)
    {
        if (velocityMagnitude <= 0f || damageMultiplier <= 0f)
        {
            return 0f;
        }

        return velocityMagnitude * damageMultiplier;
    }
}
