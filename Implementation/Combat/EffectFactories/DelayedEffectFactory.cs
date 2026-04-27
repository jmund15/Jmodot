using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;
using GCol = Godot.Collections;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Combat.EffectDefinitions;
using Effects.StatusEffects;

[GlobalClass]
public partial class DelayedEffectFactory : CombatEffectFactory
{
    [Export, RequiredExport] public PackedScene Runner { get; set; } = null!;
    [Export] public BaseFloatValueDefinition Delay { get; set; } = new ConstantFloatDefinition(1.0f);
    [Export, RequiredExport] public CombatEffectFactory EffectToApply { get; set; } = null!;
    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];
    [Export] public PackedScene? PersistentVisuals { get; set; }

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        this.ValidateRequiredExports();

        return new DelayEffect(
            Runner,
            Delay.ResolveFloatValue(stats),
            EffectToApply.Create(stats),
            PersistentVisuals,
            Tags,
            TargetVisualEffect
        );
    }
}
