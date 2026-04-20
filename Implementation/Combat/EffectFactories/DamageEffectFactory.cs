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
    [Export, RequiredExport] private BaseFloatValueDefinition _knockbackDefinition = null!;

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
    /// <summary>
    /// Multiplier for converting impact velocity to knockback force.
    /// Set to 0 for spells that use environmental force instead of combat knockback.
    /// </summary>
    [Export] public float KnockbackVelocityScaling { get; set; } = 1f;

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
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

        return new DamageEffect(finalDamage, Tags, isCritical, baseKnockback, KnockbackVelocityScaling, TargetVisualEffect);
    }
}
