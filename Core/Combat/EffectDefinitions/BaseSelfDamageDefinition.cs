namespace Jmodot.Core.Combat.EffectDefinitions;

using Stats;

/// <summary>
/// Abstract base for collision self-damage strategies.
/// Each concrete strategy receives impact velocity and stats uniformly —
/// the runner never type-checks. Strategies decide internally what to use.
/// </summary>
[GlobalClass]
public abstract partial class BaseSelfDamageDefinition : Resource
{
    /// <summary>
    /// Resolves the self-damage for a single collision event.
    /// </summary>
    /// <param name="impactVelocity">The velocity magnitude at impact (always >= 0).</param>
    /// <param name="stats">Optional stat provider for stat-driven damage values.</param>
    /// <returns>Damage amount (>= 0).</returns>
    public abstract float ResolveCollisionDamage(float impactVelocity, IStatProvider? stats);

    /// <summary>
    /// Target-aware overload — strategies that scale by the COLLIDED entity's stats
    /// (e.g. mass-scaled pierce cost) override this. Default forwards to the
    /// target-less resolution, so existing strategies are unaffected.
    /// </summary>
    /// <param name="target">The node the host collided with (may be null).</param>
    public virtual float ResolveCollisionDamage(float impactVelocity, IStatProvider? stats, Node? target)
        => ResolveCollisionDamage(impactVelocity, stats);
}
