using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Combat.Effects;

namespace Jmodot.Implementation.Combat.EffectFactories;

using System;
using Core.Stats;
using Shared;
using GCol = Godot.Collections;

[GlobalClass, Tool]
public partial class StatEffectFactory : CombatEffectFactory
{
    [Export, RequiredExport] public StatModifier Modification { get; set; } = null!;
    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];
    public override ICombatEffect Create(IStatProvider? stats = null, EffectCreationSeed? seed = null)
    {
        this.ValidateRequiredExports();
        Modification.ValidateRequiredExports();
        return new StatEffect(Modification.Attribute, Modification.Modifier, Tags, TargetVisualEffect);
    }
}
