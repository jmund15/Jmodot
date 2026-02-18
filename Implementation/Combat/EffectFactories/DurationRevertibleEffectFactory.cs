using Godot;
using Jmodot.Core.Combat;
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
    [Export] public PackedScene? RunnerOverride { get; set; }
    [Export, RequiredExport] public BaseFloatValueDefinition Duration { get; set; } = null!;
    [Export, RequiredExport] public CombatEffectFactory RevertibleEffect { get; set; } = null!;
    [Export] public GCol.Array<CombatTag> Tags { get; set; } = [];
    [Export] public PackedScene? PersistentVisuals { get; set; }

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        var runner = RunnerOverride ?? PushinPotions.Global.GlobalRegistry.DB.DefaultDurationRevertibleStatusRunner;

        // RevertibleEffect is required (= null!), fail-fast if not configured
        var effect = RevertibleEffect.Create(stats);
        if (effect is not IRevertibleCombatEffect revertibleEffect)
        {
            JmoLogger.Error(this, $"{RevertibleEffect.ResourcePath}'s effect is not revertible!");
            throw new System.InvalidOperationException($"DurationRevertibleEffectFactory: RevertibleEffect does not produce an IRevertibleCombatEffect");
        }
        return new DurationRevertEffect(
            runner,
            Duration.ResolveFloatValue(stats),
            revertibleEffect, // Use the already-cast variable instead of creating again
            PersistentVisuals,
            Tags,
            TargetVisualEffect
        );
    }
}
