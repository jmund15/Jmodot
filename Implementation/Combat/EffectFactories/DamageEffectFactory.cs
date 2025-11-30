namespace Jmodot.Implementation.Combat.EffectFactories;

using Jmodot.Core.Combat;
using Effects;

[GlobalClass]
public partial class DamageEffectFactory : CombatEffectFactory
{
    [Export] public float DamageAmount { get; set; } = 10f;

    public override ICombatEffect Create()
    {
        return new DamageEffect(DamageAmount);
    }
}
