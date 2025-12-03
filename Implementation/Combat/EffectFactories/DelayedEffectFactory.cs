using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;
using GCol = Godot.Collections;

namespace Jmodot.Implementation.Combat.EffectFactories;

[GlobalClass]
public partial class DelayedEffectFactory : CombatEffectFactory
{
    [Export] public float Delay { get; set; } = 1.0f;
    [Export] public CombatEffectFactory EffectToApply { get; set; }
    [Export] public GCol.Array<GameplayTag> Tags { get; set; } = [];
    [Export] public PackedScene PersistentVisuals { get; set; }

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        return new DelayedStatusRunner
        {
            Delay = Delay,
            Effect = EffectToApply?.Create(stats),
            Tags = Tags,
            PersistentVisuals = PersistentVisuals
        };
    }
}
