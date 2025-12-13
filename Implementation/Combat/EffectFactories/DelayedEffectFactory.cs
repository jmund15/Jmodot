using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;
using GCol = Godot.Collections;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Combat.EffectDefinitions;
using Effects.StatusEffects;

[GlobalClass]
public partial class DelayedEffectFactory : CombatEffectFactory
{
    [Export] public PackedScene Runner { get; set; } = null!;
    [Export] public FloatEffectDefinition Delay { get; set; } = new(1.0f);
    [Export] public CombatEffectFactory EffectToApply { get; set; }
    [Export] public GCol.Array<GameplayTag> Tags { get; set; } = [];
    [Export] public PackedScene PersistentVisuals { get; set; }

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        return new DelayEffect(
            Runner,
            Delay.ResolveFloatValue(stats),
            EffectToApply.Create(stats),
            PersistentVisuals,
            Tags
        );
    }
}
