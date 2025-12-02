using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;
using GCol = Godot.Collections;

namespace Jmodot.Implementation.Combat.EffectFactories;

using Shared;

[GlobalClass]
public partial class DurationRevertibleEffectFactory : CombatEffectFactory
{
    [Export] public float Duration { get; set; } = 1.0f;
    [Export] public CombatEffectFactory RevertibleEffect { get; set; } = null!;
    [Export] public GCol.Array<GameplayTag> Tags { get; set; } = [];
    [Export] public PackedScene? PersistentVisuals { get; set; }

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        var effect = RevertibleEffect?.Create(stats);
        if (effect is not IRevertibleCombatEffect revertibleEffect)
        {
            JmoLogger.Error(this, $"{RevertibleEffect.ResourcePath}'s effect is not revertible!");
            return null;
        }
        return new DurationRevertibleStatusRunner(Duration, revertibleEffect)
        {
            Tags = Tags,
            PersistentVisuals = PersistentVisuals
        };
    }
}
