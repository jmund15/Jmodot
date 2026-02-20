using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Health;
using Jmodot.Implementation.AI.BB;
using Jmodot.Core.Visual.Effects;

namespace Jmodot.Implementation.Combat.Effects;

using System.Collections.Generic;
using Core.Combat.EffectDefinitions;
using Core.Combat.Reactions;
using Shared;

/// <summary>
/// A damage effect that scales damage and knockback based on distance from the epicenter.
/// Closer targets take more damage/knockback; farther targets take less.
/// </summary>
public struct DistanceScaledDamageEffect : ICombatEffect
{
    public readonly float BaseDamage;
    public readonly float BaseKnockback;
    public readonly bool IsCritical;
    public readonly float KnockbackVelocityScaling;
    public IEnumerable<CombatTag> Tags { get; private set; }
    public VisualEffect? Visual { get; private init; }

    private readonly DistanceFalloffConfig? _damageFalloff;
    private readonly DistanceFalloffConfig? _knockbackFalloff;

    public DistanceScaledDamageEffect(
        float baseDamage,
        float baseKnockback,
        IEnumerable<CombatTag> tags,
        bool isCritical,
        DistanceFalloffConfig? damageFalloff,
        DistanceFalloffConfig? knockbackFalloff,
        float knockbackVelocityScaling = 1f,
        VisualEffect? visual = null)
    {
        BaseDamage = baseDamage;
        BaseKnockback = baseKnockback;
        Tags = tags ?? [];
        IsCritical = isCritical;
        _damageFalloff = damageFalloff;
        _knockbackFalloff = knockbackFalloff;
        KnockbackVelocityScaling = knockbackVelocityScaling;
        Visual = visual;
    }

    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        // 1. Check if we should affect this target at all
        if (_damageFalloff != null && !_damageFalloff.ShouldAffect(context.DistanceFromEpicenter))
        {
            return null;
        }

        // 2. Calculate distance-based multipliers
        float damageMultiplier = _damageFalloff?.GetMultiplier(context.DistanceFromEpicenter) ?? 1f;
        float knockbackMultiplier = _knockbackFalloff?.GetMultiplier(context.DistanceFromEpicenter) ?? 1f;

        float finalDamage = BaseDamage * damageMultiplier;
        float scaledBaseKnockback = BaseKnockback * knockbackMultiplier;

        // 3. Add velocity-based knockback
        float impactSpeed = context.ImpactVelocity.Length();
        float totalKnockback = scaledBaseKnockback + (impactSpeed * KnockbackVelocityScaling);

        // 4. Apply damage
        if (!target.Blackboard.TryGet<HealthComponent>(BBDataSig.HealthComponent, out var health))
        {
            JmoLogger.Error(this, $"Target '{target.GetUnderlyingNode().Name}' has no HealthComponent!");
            return null;
        }

        if (health == null)
        {
            JmoLogger.Error(this, $"Target '{target.GetUnderlyingNode().Name}' HealthComponent resolved to null!");
            return null;
        }

        health.TakeDamage(finalDamage, context.Attacker);

        // 5. Return result
        return new DamageResult
        {
            Source = context.Source,
            Target = target.OwnerNode,
            Tags = Tags,
            OriginalAmount = BaseDamage,
            FinalAmount = finalDamage,
            Direction = context.HitDirection,
            Force = totalKnockback,
            IsCritical = IsCritical,
            IsFatal = health.IsDead
        };
    }

}
