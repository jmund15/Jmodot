namespace Jmodot.Implementation.Actors;

/// <summary>
/// Pure static calculator for wall impact damage.
/// Damage scales linearly with velocity magnitude at impact.
/// </summary>
public static class WallImpactDamage
{
    /// <summary>
    /// Calculates damage from a wall impact based on velocity at time of collision.
    /// </summary>
    /// <param name="velocityMagnitude">Speed at impact (m/s). Negative values treated as zero.</param>
    /// <param name="damageMultiplier">Scaling factor for velocity-to-damage conversion.</param>
    /// <returns>Damage amount (always >= 0).</returns>
    public static float CalculateImpactDamage(float velocityMagnitude, float damageMultiplier)
    {
        if (velocityMagnitude <= 0f || damageMultiplier <= 0f)
        {
            return 0f;
        }

        return velocityMagnitude * damageMultiplier;
    }
}
