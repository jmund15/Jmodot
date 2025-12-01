using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Effects;

namespace Jmodot.Implementation.Combat.EffectFactories;

[GlobalClass]
public partial class DamageEffectFactory : CombatEffectFactory
{
    [Export] public float DamageAmount { get; set; } = 10.0f;
    [Export] public GameplayTag[] Tags { get; set; } = System.Array.Empty<GameplayTag>();

    public override ICombatEffect Create()
    {
        return new DamageEffect(DamageAmount, Tags);
    }
}
