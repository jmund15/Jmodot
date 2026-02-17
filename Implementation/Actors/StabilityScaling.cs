namespace Jmodot.Implementation.Actors;

/// <summary>
/// Pure static utility for stability-based resistance factor calculations.
/// Stability scales how much external force an entity physically receives.
/// Formula: resistanceFactor = 1.0 / (1.0 + stability)
///   stability=0 → factor=1.0 (full force)
///   stability=1 → factor=0.5 (half force)
///   stability=3 → factor=0.25 (quarter force)
/// Smooth diminishing returns — no hard immunity cliff.
/// </summary>
public static class StabilityScaling
{
    /// <summary>
    /// Calculates the resistance factor from a stability value.
    /// Negative stability is clamped to 0 (no force amplification).
    /// </summary>
    public static float CalculateResistanceFactor(float stability)
    {
        if (stability <= 0f)
        {
            return 1.0f;
        }

        return 1.0f / (1.0f + stability);
    }

    /// <summary>
    /// Scales a force vector by the resistance factor derived from stability.
    /// </summary>
    public static Godot.Vector3 ScaleForce(Godot.Vector3 force, float stability)
    {
        return force * CalculateResistanceFactor(stability);
    }
}
