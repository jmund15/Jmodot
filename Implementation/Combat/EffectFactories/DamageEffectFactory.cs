using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Stats;

[GlobalClass]
public partial class DamageEffectFactory : CombatEffectFactory
{
    [Export] public float Damage { get; set; } = 10f;
    [Export] public Attribute? DamageAttribute { get; set; }
    [Export] public StatOperation DamageOperation { get; set; } = StatOperation.Override;
    [Export] public GameplayTag[] Tags { get; set; } = System.Array.Empty<GameplayTag>();

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        float finalDamage = ResolveFloatValue(Damage, DamageAttribute, DamageOperation, stats);
        return new DamageEffect(finalDamage, Tags);
    }
}
