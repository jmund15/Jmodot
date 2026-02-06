namespace Jmodot.Core.Combat;

using Godot;
using System;

/// <summary>
/// A reusable Resource that calculates damage from velocity-based impacts.
/// Use for thrown objects landing, falls, collisions, etc.
///
/// Supports two calculation modes:
/// - Linear: damage = (velocity - threshold) * damagePerVelocity
/// - Curved: damage = curve.Sample(normalizedVelocity) * maxDamage
///
/// This resource is stateless (FactoryRunner pattern) - it only computes values.
/// Consumers own the state (velocity, health component references).
/// </summary>
[GlobalClass]
public partial class ImpactDamageProfile : Resource
{
    /// <summary>
    /// How damage is calculated from impact velocity.
    /// </summary>
    public enum DamageMode
    {
        /// <summary>
        /// Linear scaling: damage = (velocity - threshold) * damagePerVelocity
        /// </summary>
        Linear,

        /// <summary>
        /// Curve-based scaling: damage = curve.Sample(normalizedVelocity) * maxDamage
        /// Allows for non-linear damage curves (e.g., quadratic, soft cap, etc.)
        /// </summary>
        Curved
    }

    #region Configuration

    /// <summary>
    /// The calculation mode for impact damage.
    /// </summary>
    [Export]
    public DamageMode Mode { get; set; } = DamageMode.Linear;

    /// <summary>
    /// Minimum velocity required to cause damage.
    /// Velocities below this cause 0 damage (soft landings).
    /// </summary>
    [Export]
    public float VelocityThreshold { get; set; } = 2.0f;

    /// <summary>
    /// Maximum damage that can be dealt regardless of velocity.
    /// Prevents one-shot deaths from extreme speeds.
    /// </summary>
    [Export]
    public float MaxDamage { get; set; } = 10.0f;

    /// <summary>
    /// [Linear Mode] Damage dealt per unit of velocity above threshold.
    /// Example: 0.5 means velocity of 10 above threshold = 5 damage.
    /// </summary>
    [ExportGroup("Linear Mode")]
    [Export]
    public float DamagePerVelocity { get; set; } = 0.5f;

    /// <summary>
    /// [Curved Mode] The damage curve. X-axis is normalized velocity (0-1),
    /// Y-axis is damage multiplier (0-1) applied to MaxDamage.
    /// </summary>
    [ExportGroup("Curved Mode")]
    [Export]
    public Curve? DamageCurve { get; set; }

    /// <summary>
    /// [Curved Mode] Velocity that maps to X=1.0 on the curve.
    /// Velocities above this are clamped to 1.0.
    /// </summary>
    [Export]
    public float MaxVelocityForCurve { get; set; } = 20.0f;

    #endregion

    #region Public API

    /// <summary>
    /// Calculates impact damage for a given velocity magnitude.
    /// </summary>
    /// <param name="impactVelocity">The velocity magnitude at impact (always positive).</param>
    /// <returns>Damage to apply, clamped between 0 and MaxDamage.</returns>
    public float CalculateDamage(float impactVelocity)
    {
        // Guard: negative or zero velocity = no damage
        if (impactVelocity <= 0)
        {
            return 0f;
        }

        // Guard: below threshold = no damage
        if (impactVelocity <= VelocityThreshold)
        {
            return 0f;
        }

        float damage = Mode switch
        {
            DamageMode.Linear => CalculateLinearDamage(impactVelocity),
            DamageMode.Curved => CalculateCurvedDamage(impactVelocity),
            _ => 0f
        };

        // Clamp to [0, MaxDamage]
        return Math.Clamp(damage, 0f, MaxDamage);
    }

    #endregion

    #region Private Methods

    private float CalculateLinearDamage(float velocity)
    {
        float excessVelocity = velocity - VelocityThreshold;
        return excessVelocity * DamagePerVelocity;
    }

    private float CalculateCurvedDamage(float velocity)
    {
        if (DamageCurve == null)
        {
            return 0f;
        }

        // Normalize velocity to [0, 1] range based on MaxVelocityForCurve
        float normalizedVelocity = Math.Clamp(velocity / MaxVelocityForCurve, 0f, 1f);

        // Sample the curve and multiply by MaxDamage
        float curveValue = DamageCurve.Sample(normalizedVelocity);
        return curveValue * MaxDamage;
    }

    #endregion
}
