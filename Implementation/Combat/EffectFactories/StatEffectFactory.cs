using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Shared.Attributes;
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
    [Export, RequiredExport] public Attribute Attribute { get; set; } = null!;
    [Export, RequiredExport] public Resource Modifier { get; set; } = null!;
    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];
    public override ICombatEffect Create(IStatProvider? stats = null)
    {
        return new StatEffect(Attribute, Modifier, Tags, TargetVisualEffect);
    }
}
