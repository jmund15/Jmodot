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
[GlobalClass]
public partial class DistanceScaledDamageEffectFactory : CombatEffectFactory
{
    [Export, RequiredExport] private BaseFloatValueDefinition _damageDefinition = null!;
    [Export, RequiredExport] private BaseFloatValueDefinition _knockbackDefinition = null!;

    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];

    [ExportGroup("Distance Falloff")]
    /// <summary>
    /// If set, damage scales based on distance. Closer = more damage.
    /// </summary>
    [Export] public DistanceFalloffConfig DamageFalloff { get; set; }

    /// <summary>
    /// If set, knockback scales based on distance. Closer = more knockback.
    /// Can use same or different config as DamageFalloff.
    /// </summary>
    [Export] public DistanceFalloffConfig KnockbackFalloff { get; set; }

    [ExportGroup("Critical Hit")]
    /// <summary>
    /// Attribute for crit chance (0.0 - 1.0). If null, crit is disabled.
    /// </summary>
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
    /// <summary>
    /// Determines how much the impact speed of the projectile affects knockback.
    /// </summary>
    [Export] public float KnockbackVelocityScaling { get; set; } = 1f;

    public override ICombatEffect Create(IStatProvider? stats = null)
    {
        float baseDamage = _damageDefinition.ResolveFloatValue(stats);
        float baseKnockback = _knockbackDefinition.ResolveFloatValue(stats);

        bool isCritical = false;
        float finalDamage = baseDamage;

        // Resolve crit chance attribute: per-factory override wins, else the project-wide
        // CombatFactoryDefaults seam, else crit disabled. Both null = no crit roll (graceful).
        var critChanceAttr = CritChanceAttrOverride ?? CombatFactoryDefaults.DefaultCritChanceAttr;
        if (critChanceAttr != null && stats != null)
        {
            float critChance = stats.GetStatValue<float>(critChanceAttr);
            isCritical = JmoRng.GetRndFloat() < critChance;

            if (isCritical)
            {
                var critMultAttr = CritMultiplierAttrOverride ?? CombatFactoryDefaults.DefaultCritMultiplierAttr;
                float multiplier = critMultAttr != null
                    ? stats.GetStatValue(critMultAttr, DefaultCritMultiplier)
                    : DefaultCritMultiplier;
                finalDamage = baseDamage * multiplier;
            }
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
