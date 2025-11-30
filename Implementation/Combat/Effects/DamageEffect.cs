using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Health;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat.Effects;

using System;
using Shared;

public struct DamageEffect(float damageAmount) : ICombatEffect
{
    public readonly float DamageAmount = damageAmount;

    public void Apply(ICombatant target, HitContext context)
    {
        // Use Blackboard to get HealthComponent
        if (target.Blackboard.TryGet<HealthComponent>(BBDataSig.HealthComp, out var health))
        {
            health!.TakeDamage(DamageAmount, context.Source);
            EffectCompleted?.Invoke(this);
        }
        else
        {
            JmoLogger.Error(this, $"Target '{target.GetUnderlyingNode().Name}' has no HealthComponent!");
        }
    }
    public void Cancel()
    {
        EffectCompleted?.Invoke(this);
    }

    public event Action<ICombatEffect> EffectCompleted;
}
