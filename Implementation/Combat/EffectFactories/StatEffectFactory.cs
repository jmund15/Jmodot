using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;

namespace Jmodot.Implementation.Combat.EffectFactories;

using System;
using Core.Modifiers;
using Core.Stats;
using Shared;
using Attribute = Core.Stats.Attribute;
using GCol = Godot.Collections;

[GlobalClass]
public partial class StatEffectFactory : CombatEffectFactory
{
    [Export] public Attribute Attribute { get; set; } = null!;
    [Export] public Resource Modifier { get; set; } = null!;
    [Export] public GCol.Array<GameplayTag> Tags { get; set; } = [];
    public override ICombatEffect Create(IStatProvider? stats = null)
    {
        return new StatEffect(Attribute, Modifier, Tags);
    }
}
