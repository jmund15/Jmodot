using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Status;
using Jmodot.Core.Shared.Attributes;
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
    [Export, RequiredExport] public PackedScene Runner { get; set; } = null!;
    [Export, RequiredExport] public BaseFloatValueDefinition Duration { get; set; } = null!;
    [Export, RequiredExport] public CombatEffectFactory RevertibleEffect { get; set; } = null!;
    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];
    [Export] public PackedScene? PersistentVisuals { get; set; }
    [Export] public StatusSpreadConfig? Spread { get; set; }

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        this.ValidateRequiredExports();

        var effect = RevertibleEffect.Create(stats);
        if (effect is not IRevertibleCombatEffect revertibleEffect)
        {
            JmoLogger.Error(this, $"{RevertibleEffect.ResourcePath}'s effect is not revertible!");
            throw new System.InvalidOperationException($"DurationRevertibleEffectFactory: RevertibleEffect does not produce an IRevertibleCombatEffect");
        }
        var revertEffect = new DurationRevertEffect(
            Runner,
            Duration.ResolveFloatValue(stats),
            revertibleEffect,
            PersistentVisuals,
            Tags,
            TargetVisualEffect
        );
        revertEffect.SpreadConfig = Spread;
        return revertEffect;
    }
}
