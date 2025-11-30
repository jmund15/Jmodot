namespace Jmodot.Implementation.Combat.EffectFactories;

using Jmodot.Core.Combat;
using Effects;

[GlobalClass]
public partial class DoTEffectFactory : CombatEffectFactory
{
    [Export] public float Duration { get; set; } = 3f;
    [Export] public float TickInterval { get; set; } = 1f;
    [Export] public float DamagePerTick { get; set; } = 5f;

    public override ICombatEffect Create()
    {
        return new DoTEffect(Duration, TickInterval, DamagePerTick);
    }
}
