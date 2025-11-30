using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;

namespace Jmodot.Implementation.Combat.EffectFactories;

[GlobalClass]
public partial class TickEffectFactory : CombatEffectFactory
{
    [Export] public float Duration { get; set; } = 1.0f;
    [Export] public float Interval { get; set; } = 1.0f;
    [Export] public CombatEffectFactory EffectToApply { get; set; }
    [Export] public string[] Tags { get; set; } = System.Array.Empty<string>();

    public override ICombatEffect Create()
    {
        return new TickEffect(Duration, Interval, EffectToApply, Tags);
    }
}
