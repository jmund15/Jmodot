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
    public readonly bool IsCritical;
    /// <summary>Static force applied regardless of speed.</summary>
    public readonly float BaseKnockback;
    /// <summary>Multiplier for converting ImpactVelocity magnitude to extra force.</summary>
    public readonly float KnockbackVelocityScaling;
    public IEnumerable<CombatTag> Tags { get; private set; }

    public DamageEffect(
        float damageAmount,
        IEnumerable<CombatTag> tags,
        bool isCritical = false,
        float baseKnockback = 10f,
        float knockbackVelocityScaling = 1f)
    {
        DamageAmount = damageAmount;
        Tags = tags ?? [];
        IsCritical = isCritical;
        BaseKnockback = baseKnockback;
        KnockbackVelocityScaling = knockbackVelocityScaling;
    }

    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        // Use Blackboard to get HealthComponent
        if (target.Blackboard.TryGet<HealthComponent>(BBDataSig.HealthComponent, out var health))
        {
            // Calculate total knockback force
            // Force = Static Base + (Impact Speed * Scaling)
            float impactSpeed = context.ImpactVelocity.Length();
            float totalForceConfig = BaseKnockback + (impactSpeed * KnockbackVelocityScaling);

            //GD.Print($"damage result- direction: {context.HitDirection}; force: {totalForceConfig}");

            // Note: We are currently only CALCULATING this value.
            // The application of this force (Knockback) to the target's movement system
            // should happen here or be returned in the result for the system to handle.
            if (totalForceConfig > 0)
            {
                // TODO: Apply this force to the target's KnockbackComponent or MovementController.
                // or do we just let the receiver of the result handle it???
                // TODO: create a knockback comp that stores knockback for state machine to apply impulse when necessary
                //  or should knockback apply impulse itself??? actually not a bad idea
                // target.Blackboard.Get<IKnockbackHandler>(...)?.ApplyKnockback(context.HitDirection, totalForceConfig);
            }


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
                Direction = context.HitDirection,
                Force = totalForceConfig,
                IsCritical = IsCritical,
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
