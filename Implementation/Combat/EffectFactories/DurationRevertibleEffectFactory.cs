using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;
using GCol = Godot.Collections;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Core.Combat.EffectDefinitions;
using Effects.StatusEffects;
using Shared;

[GlobalClass]
public partial class DurationRevertibleEffectFactory : CombatEffectFactory
{
    [Export] public PackedScene Runner { get; set; } = null!;
    [Export] public FloatStatDefinition Duration { get; set; } //= new(1.0f);
    [Export] public CombatEffectFactory RevertibleEffect { get; set; } = null!;
    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];
    [Export] public PackedScene? PersistentVisuals { get; set; }

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        var effect = RevertibleEffect?.Create(stats);
        if (effect is not IRevertibleCombatEffect revertibleEffect)
        {
            JmoLogger.Error(this, $"{RevertibleEffect.ResourcePath}'s effect is not revertible!");
            return null;
        }
        return new DurationRevertEffect(
            Runner,
            Duration.ResolveFloatValue(stats),
            revertibleEffect,
            PersistentVisuals,
            Tags
        );
    }
}
