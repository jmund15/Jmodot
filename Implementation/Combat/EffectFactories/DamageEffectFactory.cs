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
/// Factory for creating DamageEffect instances.
/// Rolls for critical hit at creation time based on attacker's stats.
/// </summary>
[GlobalClass]
public partial class DamageEffectFactory : CombatEffectFactory
{
    [Export, RequiredExport] private BaseFloatValueDefinition _damageDefinition = null!;

    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];

    /// <summary>
    /// Attribute for crit chance (0.0 - 1.0). If null, crit is disabled.
    /// </summary>
    [ExportGroup("Critical Hit")]
    [Export] public Attribute? CritChanceAttrOverride { get; set; }

    /// <summary>
    /// Attribute for crit damage multiplier. Falls back to DefaultCritMultiplier if null.
    /// </summary>
    [Export] public Attribute? CritMultiplierAttrOverride { get; set; }

    /// <summary>
    /// Default crit multiplier if CritMultiplierAttribute is not set.
    /// Clamped to [1.0, 10.0] in the Inspector — values below 1.0 would
    /// reduce damage on crit (almost certainly unintended).
    /// </summary>
    [Export(PropertyHint.Range, "1.0,10.0,0.01")] public float DefaultCritMultiplier { get; set; } = 1.5f;

    [ExportGroup("Knockback")]
    [Export, RequiredExport] private BaseFloatValueDefinition _knockbackDefinition = null!;

    /// <summary>
    /// Multiplier for converting impact velocity to knockback force.
    /// Set to 0 for spells that use environmental force instead of combat knockback.
    /// </summary>
    [Export] public float KnockbackVelocityScaling { get; set; } = 1f;

    /// <summary>
    /// Optional designer-tuned falloff curves shaping knockback force by distance from
    /// the hit epicenter and angle from the source's forward axis. Both null = pass-through
    /// (force = base × velocityMult). Only the ~20% of effects with cone/AoE geometry need these.
    /// </summary>
    [ExportGroup("Spatial Falloff")]
    [Export] public Curve? DistanceFalloff { get; set; }
    [Export] public Curve? ConeAngleFalloff { get; set; }
    [Export] public float MaxRange { get; set; } = 5.0f;
    [Export] public float MaxAngleDegrees { get; set; } = 45.0f;

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null, EffectCreationSeed? seed = null)
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
            // Continuous/auto-start hitbox: defer the roll. Carry un-baked damage + crit params; the effect
            // rolls per hit from HitContext.HitSeed (seed.Seed carries the effectIdx for that derivation).
            return new DamageEffect
            {
                DamageAmount = baseDamage,
                Tags = Tags,
                Mode = CritResolution.DeferredPerHit,
                CritChance = critEnabled ? critChance : 0f,
                CritMultiplier = critMultiplier,
                CritEffectIndex = seed?.EffectIndex ?? 0,
                BaseKnockback = baseKnockback,
                KnockbackVelocityScaling = KnockbackVelocityScaling,
                DistanceFalloff = DistanceFalloff,
                ConeAngleFalloff = ConeAngleFalloff,
                MaxRange = MaxRange,
                MaxAngleDegrees = MaxAngleDegrees,
                Visual = TargetVisualEffect,
            };
        }

        // Resolved (standard attack): roll once now from the assembly-derived seed (UnseededByDesign when
        // the attacker has no entity seed — graceful; never NonDeterministic, the migration-debt marker).
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

        return new DamageEffect
        {
            DamageAmount = finalDamage,
            Tags = Tags,
            IsCritical = isCritical,
            Mode = CritResolution.Resolved,
            BaseKnockback = baseKnockback,
            KnockbackVelocityScaling = KnockbackVelocityScaling,
            DistanceFalloff = DistanceFalloff,
            ConeAngleFalloff = ConeAngleFalloff,
            MaxRange = MaxRange,
            MaxAngleDegrees = MaxAngleDegrees,
            Visual = TargetVisualEffect,
        };
    }
}
