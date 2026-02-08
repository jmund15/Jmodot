namespace Jmodot.Core.Combat.EffectDefinitions;

using Stats;

/// <summary>
/// A BaseFloatValueDefinition that calculates self-damage from impact velocity.
/// Bridges ImpactDamageProfile into the collision module's SelfDamageDefinition slot.
///
/// Unlike ConstantFloatDefinition (fixed) or AttributeFloatDefinition (stat-driven),
/// this definition uses velocity at impact time to calculate damage via a profile.
///
/// The DamageMultiplier is applied AFTER the profile's MaxDamage cap,
/// allowing per-entity scaling beyond the profile's built-in limit.
/// </summary>
[GlobalClass, Tool]
public partial class ImpactDamageDefinition : BaseFloatValueDefinition
{
    /// <summary>
    /// The profile that calculates base damage from impact velocity.
    /// Supports linear and curve-based modes.
    /// </summary>
    [Export] public ImpactDamageProfile? Profile { get; set; }

    /// <summary>
    /// Multiplier applied after the profile calculates base damage.
    /// Use for per-entity scaling (e.g., heavy ingredients take more self-damage).
    /// Applied AFTER the profile's MaxDamage cap.
    /// </summary>
    [Export] public float DamageMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Returns 0 — velocity context is required for meaningful resolution.
    /// Use <see cref="ResolveWithVelocity"/> instead.
    /// </summary>
    public override float ResolveFloatValue(IStatProvider? stats) => 0f;

    /// <summary>
    /// Resolves damage using impact velocity. Called by the collision runner
    /// when it detects this definition type on a DurableCollisionResponse.
    /// </summary>
    /// <param name="impactVelocity">The velocity magnitude at impact (always positive).</param>
    /// <returns>Final damage after profile calculation and multiplier.</returns>
    public float ResolveWithVelocity(float impactVelocity)
    {
        if (Profile == null)
        {
            return 0f;
        }

        return Profile.CalculateDamage(impactVelocity) * DamageMultiplier;
    }
}
