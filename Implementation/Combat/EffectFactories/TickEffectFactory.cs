using Godot;
using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;
using GCol = Godot.Collections;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Combat.EffectDefinitions;
using Core.Stats;
using Effects.StatusEffects;

[GlobalClass]
public partial class TickEffectFactory : CombatEffectFactory
{
    [Export] public PackedScene RunnerPrefab { get; set; } = null!;
    [Export] public BaseFloatValueDefinition Duration { get; set; } = new ConstantFloatDefinition(1.0f);
    [Export] public BaseFloatValueDefinition Interval { get; set; } = new ConstantFloatDefinition(1.0f);
    [Export] public CombatEffectFactory PerTickEffect { get; set; } = null!;
    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];
    [Export] public PackedScene? PersistentVisuals { get; set; }
    // TODO: should this be a property of the 'PerTickEffect'?
    [Export] public PackedScene? TickVisuals { get; set; }

    public override ICombatEffect Create(IStatProvider? stats = null)
    {
        // 2. Return the immutable Instruction
        return new TickEffect(
            RunnerPrefab,
            Duration.ResolveFloatValue(stats),
            Interval.ResolveFloatValue(stats),
            PerTickEffect.Create(stats),
            TickVisuals,
            PersistentVisuals,
            Tags
            );
    }
}
