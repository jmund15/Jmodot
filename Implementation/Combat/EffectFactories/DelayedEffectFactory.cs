using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;

namespace Jmodot.Implementation.Combat.EffectFactories;

[GlobalClass]
public partial class DelayedEffectFactory : CombatEffectFactory
{
    [Export] public float Delay { get; set; } = 1.0f;
    [Export] public CombatEffectFactory EffectToApply { get; set; }
    [Export] public GameplayTag[] Tags { get; set; } = System.Array.Empty<GameplayTag>();
    [Export] public PackedScene PersistentVisuals { get; set; }

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        return new DelayedStatusRunner(Delay, EffectToApply?.Create(stats))
        {
            Tags = Tags,
            PersistentVisuals = PersistentVisuals
        };
    }
}
