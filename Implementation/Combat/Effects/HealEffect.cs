namespace Jmodot.Implementation.Combat.Effects;

using System.Collections.Generic;
using AI.BB;
using Core.Combat;
using Core.Combat.Reactions;
using Core.Visual.Effects;
using Health;
using Shared;

/// <summary>
/// Combat effect that heals a percentage of the target's max health.
/// Created by <see cref="EffectFactories.HealEffectFactory"/>.
/// </summary>
public struct HealEffect : ICombatEffect
{
    public readonly float HealPercent;
    public IEnumerable<CombatTag> Tags { get; private set; }
    public VisualEffect? Visual { get; private init; }

    public HealEffect(float healPercent, VisualEffect? visual = null)
    {
        HealPercent = healPercent;
        Tags = [];
        Visual = visual;
    }

    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        if (!target.Blackboard.TryGet<HealthComponent>(BBDataSig.HealthComponent, out var health) || health == null)
        {
            JmoLogger.Warning(this, $"Target '{target.GetUnderlyingNode().Name}' has no HealthComponent!");
            return null;
        }

        float amount = health.MaxHealth * HealPercent;
        if (amount <= 0) { return null; }

        health.Heal(amount, context.Source);

        return new HealResult
        {
            Source = context.Source,
            Target = target.OwnerNode,
            Tags = Tags,
            AmountHealed = amount
        };
    }
}
