using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Combat.EffectDefinitions;
using Core.Stats;
using PushinPotions.Global;
using GCol = Godot.Collections;


/// <summary>
/// Factory for creating DamageEffect instances.
/// Rolls for critical hit at creation time based on attacker's stats.
/// </summary>
[GlobalClass]
public partial class DamageEffectFactory : CombatEffectFactory
{
    [Export] private FloatStatDefinition _damageDefinition = null!;
    [Export] private FloatStatDefinition _knockbackDefinition = null!;

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
    /// </summary>
    [Export] public float DefaultCritMultiplier { get; set; } = 1.5f;

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        float baseDamage = _damageDefinition.ResolveFloatValue(stats);
        float baseKnockback = _knockbackDefinition.ResolveFloatValue(stats);

        bool isCritical = false;
        float finalDamage = baseDamage;

        // Roll for crit if CritChanceAttribute is configured (not default/null resource)
        if (CritChanceAttrOverride != null && stats != null)
        {
            float critChance = stats.GetStatValue<float>(CritChanceAttrOverride ?? GlobalRegistry.DB.CriticalChanceAttr);
            isCritical = System.Random.Shared.NextSingle() < critChance;

            if (isCritical)
            {
                var multiplier = stats.GetStatValue(CritMultiplierAttrOverride ?? GlobalRegistry.DB.CriticalMultiplierAttr, DefaultCritMultiplier);
                finalDamage = baseDamage * multiplier;
            }
        }

        // determines how much the impact speed of the projectile impacts the knockback.
        // I'm guessing we want to make this a variable at some point?
        var knockbackVelocityScaling = 1f;

        return new DamageEffect(finalDamage, Tags, isCritical, baseKnockback, knockbackVelocityScaling);
    }
}
