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
public partial class DurationEffectFactory : CombatEffectFactory
{
    [Export] public PackedScene? RunnerOverride { get; set; }
    [Export] public BaseFloatValueDefinition Duration { get; set; } = new ConstantFloatDefinition(1.0f);
    [Export] public CombatEffectFactory? OnStartEffect { get; set; }
    [Export] public CombatEffectFactory? OnEndEffect { get; set; }
    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];
    [Export] public PackedScene? PersistentVisuals { get; set; }

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        var runner = RunnerOverride ?? PushinPotions.Global.GlobalRegistry.DB.DefaultDurationStatusRunner;

        return new DurationEffect(
            runner,
            Duration.ResolveFloatValue(stats),
            OnStartEffect?.Create(stats),
            OnEndEffect?.Create(stats),
            PersistentVisuals,
            Tags = Tags,
            TargetVisualEffect
        );
    }
}
