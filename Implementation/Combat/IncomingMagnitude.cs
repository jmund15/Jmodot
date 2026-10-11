namespace Jmodot.Implementation.Combat;

using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Health;
using Jmodot.Implementation.Shared;

/// <summary>
/// Resolves and applies the incoming-magnitude operand that scales a hit's damage-bearing effects
/// on one defender. A path that forwards a hit or a damage-over-time tick resolves the operand with
/// <see cref="Resolve"/>; a damage effect applies it with <see cref="ApplyDamage"/>.
/// </summary>
public static class IncomingMagnitude
{
    /// <summary>
    /// Resolves the operand for <paramref name="attackerNode"/> striking <paramref name="defender"/>
    /// through <see cref="CombatFactoryDefaults.EffectivenessResolver"/>, reading both identities at
    /// call time. Returns <c>1.0f</c> when no resolver is wired, the attacker is null or freed, or
    /// either side has no <see cref="Jmodot.Core.Identification.IIdentifiable"/> ancestor.
    /// </summary>
    public static float Resolve(Node? attackerNode, ICombatant defender)
    {
        var resolver = CombatFactoryDefaults.EffectivenessResolver;
        if (resolver == null) { return 1.0f; }
        if (attackerNode == null || !GodotObject.IsInstanceValid(attackerNode)) { return 1.0f; }

        var attackerIdentity = attackerNode.TryResolveIdentifiable()?.GetIdentity();
        if (attackerIdentity == null) { return 1.0f; }

        var defenderNode = defender.OwnerNode;
        var defenderIdentity = defenderNode.TryResolveIdentifiable()?.GetIdentity();
        if (defenderIdentity == null) { return 1.0f; }

        return resolver.Resolve(attackerIdentity, defenderIdentity, attackerNode, defenderNode);
    }

    /// <summary>
    /// Applies <paramref name="amount"/> scaled by <paramref name="incomingMagnitudeScale"/> to
    /// <paramref name="health"/> and returns the damage actually dealt. Any operand that is not a
    /// finite positive number fully suppresses the hit: health raises
    /// <see cref="HealthComponent.OnHitSuppressed"/> instead of taking damage, and the return is 0.
    /// </summary>
    public static float ApplyDamage(HealthComponent health, float amount, float incomingMagnitudeScale, HitContext context)
    {
        if (!(incomingMagnitudeScale > 0f) || !float.IsFinite(incomingMagnitudeScale))
        {
            if (incomingMagnitudeScale != 0f)
            {
                JmoLogger.Warning(typeof(IncomingMagnitude),
                    $"Out-of-contract incoming-magnitude operand {incomingMagnitudeScale}; the hit is treated as immune.");
            }
            health.NotifyHitSuppressed(context.Attacker, context.Kind);
            return 0f;
        }

        float applied = amount * incomingMagnitudeScale;
        health.TakeDamage(applied, context.Attacker, context.Kind, context.ImpactDirection, incomingMagnitudeScale);
        return applied;
    }
}
