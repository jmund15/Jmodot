using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Shared;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Combat.EffectDefinitions;
using Core.Stats;
using GCol = Godot.Collections;

/// <summary>
/// Factory for creating distance-scaled damage effects.
/// Damage and knockback scale based on proximity to the hitbox epicenter.
/// </summary>
[GlobalClass, Tool]
public partial class DistanceScaledDamageEffectFactory : CombatEffectFactory
{
    [Export, RequiredExport] private BaseFloatValueDefinition _damageDefinition = null!;
    [Export, RequiredExport] private BaseFloatValueDefinition _knockbackDefinition = null!;

    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];

    /// <summary>
    /// If set, damage scales based on distance. Closer = more damage.
    /// </summary>
    [ExportGroup("Distance Falloff")]
    [Export] public DistanceFalloffConfig DamageFalloff { get; set; }

    /// <summary>
    /// If set, knockback scales based on distance. Closer = more knockback.
    /// May reuse the same config as <see cref="DamageFalloff"/> or use a different one.
    /// </summary>
    [Export] public DistanceFalloffConfig KnockbackFalloff { get; set; }

    /// <summary>
    /// Attribute for crit chance (0.0 - 1.0). If null, crit is disabled.
    /// </summary>
    [ExportGroup("Critical Hit")]
    [Export] public Attribute? CritChanceAttrOverride { get; set; }

    /// <summary>
    /// Attribute for crit damage multiplier. Falls back to <see cref="DefaultCritMultiplier"/> if null.
    /// </summary>
    [Export] public Attribute? CritMultiplierAttrOverride { get; set; }

    /// <summary>
    /// Default crit multiplier used when <see cref="CritMultiplierAttrOverride"/> is not set.
    /// Clamped to [1.0, 10.0] in the Inspector — values below 1.0 would
    /// reduce damage on crit (almost certainly unintended).
    /// </summary>
    [Export(PropertyHint.Range, "1.0,10.0,0.01")] public float DefaultCritMultiplier { get; set; } = 1.5f;

    /// <summary>
    /// Determines how much the impact speed of the projectile affects knockback.
    /// </summary>
    [ExportGroup("Knockback")]
    [Export] public float KnockbackVelocityScaling { get; set; } = 1f;

    public override ICombatEffect Create(IStatProvider? stats = null, EffectCreationSeed? seed = null)
    {
        this.ValidateRequiredExports();

        float baseDamage = _damageDefinition.ResolveFloatValue(stats);
        float baseKnockback = _knockbackDefinition.ResolveFloatValue(stats);

        // Resolve crit attributes: per-factory override wins, else the project-wide CombatFactoryDefaults
        // seam, else crit disabled. Both null = no crit (graceful).
        var critChanceAttr = CritChanceAttrOverride ?? CombatFactoryDefaults.DefaultCritChanceAttr;
        bool critEnabled = critChanceAttr != null && stats != null;
        float critChance = critEnabled ? stats!.GetStatValue<float>(critChanceAttr!) : 0f;
        var critMultAttr = CritMultiplierAttrOverride ?? CombatFactoryDefaults.DefaultCritMultiplierAttr;
        float critMultiplier = (critMultAttr != null && stats != null)
            ? stats.GetStatValue(critMultAttr, DefaultCritMultiplier)
            : DefaultCritMultiplier;

        if ((seed?.Resolution ?? CritResolution.Resolved) == CritResolution.DeferredPerHit)
        {
            // Continuous hitbox: defer the roll to Apply (un-baked damage; crit params carried).
            return new DistanceScaledDamageEffect(
                baseDamage,
                baseKnockback,
                Tags,
                false,
                DamageFalloff,
                KnockbackFalloff,
                KnockbackVelocityScaling,
                TargetVisualEffect,
                CritResolution.DeferredPerHit,
                critEnabled ? critChance : 0f,
                critMultiplier,
                seed?.EffectIndex ?? 0);
        }

        // Resolved: roll once now from the assembly-derived seed (UnseededByDesign when unseeded;
        // never NonDeterministic). Crit multiplier baked into baseDamage, mirroring legacy behavior.
        bool isCritical = false;
        float finalDamage = baseDamage;
        if (critEnabled)
        {
            float roll = seed.HasValue
                ? new JmoRng(seed.Value.CritRollSeed).GetRndFloat()
                : JmoRng.UnseededByDesign().GetRndFloat();
            isCritical = CritResolver.Resolve(roll, critChance);
            if (isCritical) { finalDamage = baseDamage * critMultiplier; }
        }

        return new DistanceScaledDamageEffect(
            finalDamage,
            baseKnockback,
            Tags,
            isCritical,
            DamageFalloff,
            KnockbackFalloff,
            KnockbackVelocityScaling,
            TargetVisualEffect
        );
    }
}
