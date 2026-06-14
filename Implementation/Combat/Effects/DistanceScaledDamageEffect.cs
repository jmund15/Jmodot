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
    public readonly CritResolution Mode;
    public readonly float CritChance;
    public readonly float CritMultiplier;
    public readonly int CritEffectIndex;
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
        VisualEffect? visual = null,
        CritResolution mode = CritResolution.Resolved,
        float critChance = 0f,
        float critMultiplier = 1f,
        int critEffectIndex = 0)
    {
        BaseDamage = baseDamage;
        BaseKnockback = baseKnockback;
        Tags = tags ?? [];
        IsCritical = isCritical;
        _damageFalloff = damageFalloff;
        _knockbackFalloff = knockbackFalloff;
        KnockbackVelocityScaling = knockbackVelocityScaling;
        Visual = visual;
        Mode = mode;
        CritChance = critChance;
        CritMultiplier = critMultiplier;
        CritEffectIndex = critEffectIndex;
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

        // Resolved: crit baked into BaseDamage at the factory. DeferredPerHit: roll now from this hit's
        // lineage seed and apply the multiplier after distance scaling (product is commutative with the
        // Resolved order, so the final number matches base*crit*distance either way).
        bool isCritical = IsCritical;
        if (Mode == CritResolution.DeferredPerHit)
        {
            isCritical = RollDeferredCrit(context);
            if (isCritical) { finalDamage *= CritMultiplier; }
        }

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

        health.TakeDamage(finalDamage, context.Attacker, context.Kind);

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
            IsCritical = isCritical,
            IsFatal = health.IsDead
        };
    }

    private bool RollDeferredCrit(HitContext context)
    {
        float roll = context.HitSeed.HasValue
            ? new JmoRng(SeedManager.DeriveChild(context.HitSeed.Value, SeedKinds.Crit, CritEffectIndex)).GetRndFloat()
            : JmoRng.UnseededByDesign().GetRndFloat();
        return CritResolver.Resolve(roll, CritChance);
    }
}
