using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Health;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat.Effects;

using System;
using System.Collections.Generic;
using Core.Combat.Reactions;
using Shared;

public struct DamageEffect : ICombatEffect
{
    public readonly float DamageAmount;
    public IEnumerable<CombatTag> Tags { get; private set; }

    public DamageEffect(float damageAmount, IEnumerable<CombatTag> tags)
    {
        DamageAmount = damageAmount;
        Tags = tags ?? [];
    }

    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        // Use Blackboard to get HealthComponent
        if (target.Blackboard.TryGet<HealthComponent>(BBDataSig.HealthComponent, out var health))
        {
            // TODO: add any damage modifiers here!! this includes target's armor, weakness, etc.

            // TODO: add in modifier 'modules' in a list to damageeffectfactory, which dictates the
            //     types of modifiers that can effect the final value from the target.
            //      Potentially, there could be a list of these modules on the target itself, or attached to the effect
            //      if the effect can't be affected by armor (true damage), then it shouldn't have the armor module, etc.

            health!.TakeDamage(DamageAmount, context.Source);

            // Result Generation
            return new DamageResult
            {
                Source = context.Source,
                Target = target.OwnerNode,
                Tags = Tags,
                OriginalAmount = DamageAmount,
                FinalAmount = DamageAmount,
                IsFatal = health.IsDead
            };
        }
        else
        {
            JmoLogger.Error(this, $"Target '{target.GetUnderlyingNode().Name}' has no HealthComponent!");
            return null;
        }
    }
    public void Cancel()
    {
        EffectCompleted?.Invoke(this, false);
    }

    public event Action<ICombatEffect, bool> EffectCompleted;
}
