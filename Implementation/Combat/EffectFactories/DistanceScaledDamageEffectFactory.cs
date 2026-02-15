using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Shared;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Combat.EffectDefinitions;
using Core.Stats;
using PushinPotions.Global;
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
    /// </summary>
    [Export] public float DefaultCritMultiplier { get; set; } = 1.5f;

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

        // Roll for crit if CritChanceAttribute is configured
        if (CritChanceAttrOverride != null && stats != null)
        {
            float critChance = stats.GetStatValue<float>(CritChanceAttrOverride);
            isCritical = MiscUtils.GetRndFloat() < critChance;

            if (isCritical)
            {
                var multiplier = stats.GetStatValue(CritMultiplierAttrOverride ?? GlobalRegistry.DB.CriticalMultiplierAttr, DefaultCritMultiplier);
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
