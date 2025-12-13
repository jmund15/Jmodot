using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Combat.EffectDefinitions;
using Core.Stats;
using GCol = Godot.Collections;

[GlobalClass]
public partial class DamageEffectFactory : CombatEffectFactory
{
    [Export] private FloatEffectDefinition _floatEffectDefinition = null!;
    [Export] public GCol.Array<GameplayTag> Tags { get; set; } = [];

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        float damage = _floatEffectDefinition.ResolveFloatValue(stats);
        return new DamageEffect(damage, Tags);
    }
}
