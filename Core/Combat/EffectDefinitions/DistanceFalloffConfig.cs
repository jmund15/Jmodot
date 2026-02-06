namespace Jmodot.Core.Combat.EffectDefinitions;

using Jmodot.Core.Shared.Attributes;

/// <summary>
/// Configuration resource for distance-based effect falloff.
/// Define how damage/knockback/etc scales based on distance from epicenter.
/// </summary>
[GlobalClass]
public partial class DistanceFalloffConfig : Resource
{
    /// <summary>
    /// The falloff curve. X-axis: Normalized distance (0=epicenter, 1=MaxRadius).
    /// Y-axis: Multiplier (0-1+). If null, returns 1.0 (no falloff).
    /// </summary>
    [Export, RequiredExport] public Curve FalloffCurve { get; set; } = null!;

    /// <summary>
    /// The radius for normalization. At distance >= MaxRadius, curve samples at X=1.
    /// </summary>
    [Export] public float MaxRadius { get; set; } = 5f;

    /// <summary>
    /// If false, targets beyond MaxRadius receive no effect at all.
    /// If true, they receive the curve's value at X=1.
    /// </summary>
    [Export] public bool AffectsBeyondRadius { get; set; } = true;

    /// <summary>
    /// Samples the falloff curve at the given distance.
    /// Returns 1.0 if no curve is set (full effect).
    /// </summary>
    /// <param name="distance">The absolute distance from epicenter.</param>
    /// <returns>The multiplier to apply to the effect (0.0 to 1.0+).</returns>
    public float GetMultiplier(float distance)
    {
        if (FalloffCurve == null)
        {
            return 1f;
        }

        float normalizedDistance = Mathf.Clamp(distance / MaxRadius, 0f, 1f);
        return FalloffCurve.Sample(normalizedDistance);
    }

    /// <summary>
    /// Checks if a target at the given distance should be affected at all.
    /// </summary>
    /// <param name="distance">The absolute distance from epicenter.</param>
    /// <returns>True if the target should receive the effect.</returns>
    public bool ShouldAffect(float distance)
    {
        if (AffectsBeyondRadius)
        {
            return true;
        }

        return distance <= MaxRadius;
    }
}
