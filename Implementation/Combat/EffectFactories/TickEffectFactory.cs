using Godot;
using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;
using GCol = Godot.Collections;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Combat.EffectDefinitions;
using Core.Stats;
using Core.Visual.Effects;
using Effects.StatusEffects;

[GlobalClass]
public partial class TickEffectFactory : CombatEffectFactory
{
    [Export] public PackedScene? RunnerOverride { get; set; }
    [Export] public BaseFloatValueDefinition Duration { get; set; } = new ConstantFloatDefinition(1.0f);
    [Export] public BaseFloatValueDefinition Interval { get; set; } = new ConstantFloatDefinition(1.0f);
    [Export, RequiredExport] public CombatEffectFactory PerTickEffect { get; set; } = null!;
    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];
    [Export] public PackedScene? PersistentVisuals { get; set; }
    // TODO: should this be a property of the 'PerTickEffect'?
    [Export] public PackedScene? TickVisuals { get; set; }
    [Export] public VisualEffect? TickVisualEffect { get; set; }

    public override ICombatEffect Create(IStatProvider? stats = null)
    {
        var runner = RunnerOverride ?? PushinPotions.Global.GlobalRegistry.DB.DefaultTickStatusRunner;

        // 2. Return the immutable Instruction
        return new TickEffect(
            runner,
            Duration.ResolveFloatValue(stats),
            Interval.ResolveFloatValue(stats),
            PerTickEffect.Create(stats),
            TickVisuals,
            PersistentVisuals,
            Tags,
            TargetVisualEffect,
            TickVisualEffect
            );
    }
}
