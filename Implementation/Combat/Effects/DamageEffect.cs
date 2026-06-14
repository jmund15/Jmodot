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

/// <summary>
/// Combat-layer effect that applies damage to a target's HealthComponent and reports
/// the resulting force/critical state via a DamageResult. Knockback magnitude is
/// computed by <see cref="KnockbackForceResolver.Resolve"/> from the base + spatial
/// falloff curves + impact velocity.
/// </summary>
/// <remarks>
/// Constructed via named-property init by <c>DamageEffectFactory.Create</c>. A class
/// (not struct) because <c>ICombatEffect</c> reference semantics box-through anyway —
/// the struct optimization bought nothing and made the 10-arg positional ctor fragile
/// (silent param-order swap risk: <c>BaseKnockback</c> ↔ <c>MaxRange</c> are both
/// <c>float</c>).
/// </remarks>
public class DamageEffect : ICombatEffect
{
    public float DamageAmount { get; init; }
    public bool IsCritical { get; init; }

    /// <summary>Resolve crit at assembly (<see cref="CritResolution.Resolved"/>, default — <see cref="IsCritical"/>
    /// pre-rolled and <see cref="DamageAmount"/> already multiplied) or per-hit at <see cref="Apply"/>
    /// (<see cref="CritResolution.DeferredPerHit"/> — roll from <c>HitContext.HitSeed</c>, multiply here).</summary>
    public CritResolution Mode { get; init; } = CritResolution.Resolved;

    /// <summary>Crit chance for the deferred roll (ignored when <see cref="Mode"/> is Resolved).</summary>
    public float CritChance { get; init; }

    /// <summary>Damage multiplier applied on a deferred crit (ignored when Resolved).</summary>
    public float CritMultiplier { get; init; } = 1f;

    /// <summary>Per-effect index folded into the deferred crit derivation
    /// (<c>DeriveChild(hitSeed,"crit",CritEffectIndex)</c>) so multiple damage effects on one hit roll independently.</summary>
    public int CritEffectIndex { get; init; }

    /// <summary>Static force applied regardless of impact speed (units/sec²).</summary>
    public float BaseKnockback { get; init; }

    /// <summary>Multiplier for converting ImpactVelocity magnitude to extra force.</summary>
    public float KnockbackVelocityScaling { get; init; } = 1f;

    /// <summary>Optional distance-falloff curve sampled at <c>distance/MaxRange ∈ [0,1]</c>.</summary>
    public Curve? DistanceFalloff { get; init; }

    /// <summary>Optional angle-falloff curve sampled at <c>angle/MaxAngleDegrees ∈ [0,1]</c>.</summary>
    public Curve? ConeAngleFalloff { get; init; }

    /// <summary>Distance normalizer for <see cref="DistanceFalloff"/> (meters).</summary>
    public float MaxRange { get; init; } = 5.0f;

    /// <summary>Angle normalizer for <see cref="ConeAngleFalloff"/> (degrees, half-angle).</summary>
    public float MaxAngleDegrees { get; init; } = 45.0f;

    public IEnumerable<CombatTag> Tags { get; init; } = [];
    public VisualEffect? Visual { get; init; }

    /// <summary>Init-syntax constructor: <c>new DamageEffect { DamageAmount = ..., ... }</c>.</summary>
    public DamageEffect() { }

    /// <summary>
    /// Positional constructor preserved for backwards compatibility with existing
    /// callers (factories, test fixtures). New code SHOULD prefer the init-syntax
    /// constructor — both <c>BaseKnockback</c>/<c>MaxRange</c> are <c>float</c> and
    /// positional swaps compile silently.
    /// </summary>
    public DamageEffect(
        float damageAmount,
        IEnumerable<CombatTag> tags,
        bool isCritical = false,
        float baseKnockback = 10f,
        float knockbackVelocityScaling = 1f,
        Curve? distanceFalloff = null,
        Curve? coneAngleFalloff = null,
        float maxRange = 5.0f,
        float maxAngleDegrees = 45.0f,
        VisualEffect? visual = null)
    {
        DamageAmount = damageAmount;
        Tags = tags ?? [];
        IsCritical = isCritical;
        BaseKnockback = baseKnockback;
        KnockbackVelocityScaling = knockbackVelocityScaling;
        DistanceFalloff = distanceFalloff;
        ConeAngleFalloff = coneAngleFalloff;
        MaxRange = maxRange;
        MaxAngleDegrees = maxAngleDegrees;
        Visual = visual;
    }

    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        if (!target.Blackboard.TryGet<HealthComponent>(BBDataSig.HealthComponent, out var health) || health == null)
        {
            // Soft no-op: VFX-only dummy targets and environmental hit-receivers do not
            // carry a HealthComponent. Demoted from Error so test fixtures and incidental
            // hits do not trip the project-wide JmoLogger.Error → test-fail contract.
            JmoLogger.Warning(this,
                $"DamageEffect.Apply soft no-op: target '{target.GetUnderlyingNode().Name}' has no HealthComponent.");
            return null;
        }

        float totalForce = KnockbackForceResolver.Resolve(
            BaseKnockback,
            DistanceFalloff, MaxRange,
            ConeAngleFalloff, MaxAngleDegrees,
            context,
            KnockbackVelocityScaling);

        // Resolved: crit was rolled at assembly — IsCritical is set and DamageAmount already includes
        // the multiplier (behavior-preserving). DeferredPerHit: roll now from this hit's lineage seed so
        // each tick of a continuous attack crits independently, then apply the multiplier here.
        bool isCritical = IsCritical;
        float appliedDamage = DamageAmount;
        if (Mode == CritResolution.DeferredPerHit)
        {
            isCritical = RollDeferredCrit(context);
            appliedDamage = isCritical ? DamageAmount * CritMultiplier : DamageAmount;
        }

        health.TakeDamage(appliedDamage, context.Attacker, context.Kind);

        return new DamageResult
        {
            Source = context.Source,
            Target = target.OwnerNode,
            Tags = Tags,
            OriginalAmount = DamageAmount,
            FinalAmount = appliedDamage,
            Direction = context.HitDirection,
            Force = totalForce,
            IsCritical = isCritical,
            IsFatal = health.IsDead
        };
    }

    private bool RollDeferredCrit(HitContext context)
    {
        // JmoRng allocates here (apply time, Godot-runtime path) — never on the Resolved branch, so
        // pure-CLR Resolved-effect tests stay JmoRng-free. Null HitSeed → UnseededByDesign (graceful,
        // silent; the hurtbox already warned at ResolveHitSeed). Never NonDeterministic (migration-debt marker).
        float roll = context.HitSeed.HasValue
            ? new JmoRng(SeedManager.DeriveChild(context.HitSeed.Value, SeedKinds.Crit, CritEffectIndex)).GetRndFloat()
            : JmoRng.UnseededByDesign().GetRndFloat();
        return CritResolver.Resolve(roll, CritChance);
    }
}
