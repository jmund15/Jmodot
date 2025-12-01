using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Health;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat.Effects;

using System;
using Shared;

public struct DamageEffect : ICombatEffect
{
    public readonly float DamageAmount;
    public GameplayTag[] Tags { get; private set; }

    public DamageEffect(float damageAmount, GameplayTag[] tags)
    {
        DamageAmount = damageAmount;
        Tags = tags ?? Array.Empty<GameplayTag>();
    }

    public void Apply(ICombatant target, HitContext context)
    {
        // Use Blackboard to get HealthComponent
        if (target.Blackboard.TryGet<HealthComponent>(BBDataSig.HealthComponent, out var health))
        {
            health!.TakeDamage(DamageAmount, context.Source);
            EffectCompleted?.Invoke(this, true);
        }
        else
        {
            JmoLogger.Error(this, $"Target '{target.GetUnderlyingNode().Name}' has no HealthComponent!");
        }
    }
    public void Cancel()
    {
        EffectCompleted?.Invoke(this, false);
    }

    public event Action<ICombatEffect, bool> EffectCompleted;
}
