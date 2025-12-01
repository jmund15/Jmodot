using Godot;
using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;

namespace Jmodot.Implementation.Combat.EffectFactories;

[GlobalClass]
public partial class TickEffectFactory : CombatEffectFactory
{
    [Export] public float Duration { get; set; } = 1.0f;
    [Export] public float Interval { get; set; } = 1.0f;
    [Export] public CombatEffectFactory EffectToApply { get; set; }
    [Export] public GameplayTag[] Tags { get; set; } = System.Array.Empty<GameplayTag>();
    [Export] public PackedScene PersistentVisuals { get; set; }
    [Export] public PackedScene TickVisuals { get; set; }

    public override ICombatEffect Create()
    {
        return new TickStatusRunner(Duration, Interval, EffectToApply, Tags)
        {
            PersistentVisuals = PersistentVisuals,
            TickVisuals = TickVisuals
        };
    }
}
