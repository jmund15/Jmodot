using Godot;
using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;
using GCol = Godot.Collections;

namespace Jmodot.Implementation.Combat.EffectFactories;

[GlobalClass]
public partial class TickEffectFactory : CombatEffectFactory
{
    [Export] public float Duration { get; set; } = 1.0f;
    [Export] public float Interval { get; set; } = 1.0f;
    [Export] public CombatEffectFactory EffectToApply { get; set; }
    [Export] public GCol.Array<GameplayTag> Tags { get; set; } = [];
    [Export] public PackedScene PersistentVisuals { get; set; }
    [Export] public PackedScene TickVisuals { get; set; }

    public override ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null)
    {
        return new TickStatusRunner(Duration, Interval, EffectToApply.Create(stats), Tags)
        {
            PersistentVisuals = PersistentVisuals,
            TickVisuals = TickVisuals
        };
    }
}
