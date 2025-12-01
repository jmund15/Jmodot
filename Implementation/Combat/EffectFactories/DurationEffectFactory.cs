using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;
using Jmodot.Implementation.Combat.Status;

namespace Jmodot.Implementation.Combat.EffectFactories;

[GlobalClass]
public partial class DurationEffectFactory : CombatEffectFactory
{
    [Export] public float Duration { get; set; } = 1.0f;
    [Export] public CombatEffectFactory OnStartEffect { get; set; }
    [Export] public CombatEffectFactory OnEndEffect { get; set; }
    [Export] public GameplayTag[] Tags { get; set; } = System.Array.Empty<GameplayTag>();
    [Export] public PackedScene PersistentVisuals { get; set; }

    public override ICombatEffect Create()
    {
        return new DurationStatusRunner(Duration, OnStartEffect, OnEndEffect)
        {
            Tags = Tags,
            PersistentVisuals = PersistentVisuals
        };
    }
}
