using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Combat.Effects;

namespace Jmodot.Implementation.Combat.EffectFactories;

/// <summary>
/// A factory that creates DamageEffect instances with damage values resolved from an IStatProvider.
/// This allows spell damage to be calculated dynamically based on the caster's stats at spell creation time.
/// </summary>
[GlobalClass]
public partial class StatBasedDamageEffectFactory : StatContextEffectFactory
{
    [Export] public Attribute DamageAttribute { get; set; } = null!;
    [Export] public GameplayTag[] Tags { get; set; } = System.Array.Empty<GameplayTag>();

    /// <summary>
    /// Creates a DamageEffect with the damage value resolved from the provided IStatProvider.
    /// </summary>
    public override ICombatEffect Create(IStatProvider stats)
    {
        if (stats == null)
        {
            throw new System.ArgumentNullException(nameof(stats), 
                $"{nameof(StatBasedDamageEffectFactory)} requires a valid IStatProvider.");
        }

        if (DamageAttribute == null)
        {
            throw new System.InvalidOperationException(
                $"{nameof(StatBasedDamageEffectFactory)} requires DamageAttribute to be set.");
        }

        float damageValue = stats.GetStatValue<float>(DamageAttribute, 0f);
        return new DamageEffect(damageValue, Tags);
    }
}
