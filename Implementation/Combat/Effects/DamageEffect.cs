using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Health;
using Jmodot.Implementation.AI.BB;
using Jmodot.Core.Visual.Effects;

namespace Jmodot.Implementation.Combat.Effects;

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
    public VisualEffect? Visual { get; private init; }

    public DamageEffect(
        float damageAmount,
        IEnumerable<CombatTag> tags,
        bool isCritical = false,
        float baseKnockback = 10f,
        float knockbackVelocityScaling = 1f,
        VisualEffect? visual = null)
    {
        DamageAmount = damageAmount;
        Tags = tags ?? [];
        IsCritical = isCritical;
        BaseKnockback = baseKnockback;
        KnockbackVelocityScaling = knockbackVelocityScaling;
        Visual = visual;
    }

    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        if (!target.Blackboard.TryGet<HealthComponent>(BBDataSig.HealthComponent, out var health) || health == null)
        {
            JmoLogger.Error(this, $"Target '{target.GetUnderlyingNode().Name}' has no HealthComponent!");
            return null;
        }

        // Calculate total knockback force: Static Base + (Impact Speed * Scaling)
        float impactSpeed = context.ImpactVelocity.Length();
        float totalForceConfig = BaseKnockback + (impactSpeed * KnockbackVelocityScaling);

        // TODO: add damage modifier modules (armor, weakness, true damage, etc.)

        health.TakeDamage(DamageAmount, context.Attacker);

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
}
