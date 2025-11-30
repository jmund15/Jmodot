using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;

namespace Jmodot.Implementation.Combat.EffectFactories;

[GlobalClass]
public partial class DurationEffectFactory : CombatEffectFactory
{
    [Export] public float Duration { get; set; } = 1.0f;
    [Export] public CombatEffectFactory OnStartEffect { get; set; }
    [Export] public CombatEffectFactory OnEndEffect { get; set; }
    [Export] public string[] Tags { get; set; } = System.Array.Empty<string>();

    public override ICombatEffect Create()
    {
        return new DurationEffect(Duration, OnStartEffect, OnEndEffect, Tags);
    }
}
