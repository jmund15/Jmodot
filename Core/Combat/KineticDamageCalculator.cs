namespace Jmodot.Core.Combat;

/// <summary>
/// Pure mass × velocity² damage scaling for charge-class attacks.
/// <para>
/// Heavy / fast chargers do more damage; light / slow chargers do less.
/// Callers compute <c>damageAmount = Compute(baseDamage, mass, velocity, k)</c>
/// before constructing a <see cref="Effects.DamageEffect"/> tagged with the
/// <c>kinetic_damage</c> <see cref="CombatTag"/>; the resulting damage scales
/// physically rather than via designer-authored gates.
/// </para>
/// <para>
/// Stateless and side-effect-free. Damage modifiers (armor, weakness, kinetic
/// affinity, normalization tuning) live in the damage-modifier-modules layer
/// flagged in <c>DamageEffect.cs</c>'s TODO, not inside this calculator.
/// </para>
/// </summary>
public static class KineticDamageCalculator
{
    /// <summary>
    /// Computes <c>baseDamage × mass × velocity² × k</c>.
    /// </summary>
    /// <param name="baseDamage">The undiluted damage amount the caller wants to scale.</param>
    /// <param name="mass">Attacker mass (kg-equivalent in the physics frame).</param>
    /// <param name="velocity">Attacker speed at impact (m/s, scalar).</param>
    /// <param name="k">Normalization constant — converts physics units into damage units; tuned per game.</param>
    /// <returns>Scaled damage amount; non-negative when all inputs are non-negative.</returns>
    public static float Compute(float baseDamage, float mass, float velocity, float k)
    {
        return baseDamage * mass * (velocity * velocity) * k;
    }
}
