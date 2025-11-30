namespace Jmodot.Implementation.Combat.EffectFactories;

using Jmodot.Core.Combat;
using Effects;

[GlobalClass]
public partial class StunEffectFactory : CombatEffectFactory
{
    [Export] public float Duration { get; set; } = 2f;

    public override ICombatEffect Create()
    {
        return new StunEffect(Duration);
    }
}
