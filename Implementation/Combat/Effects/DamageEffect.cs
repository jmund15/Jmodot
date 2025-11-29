using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Health;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat.Effects;

[GlobalClass]
public partial class DamageEffect : CombatEffect
{
    [Export]
    public float DamageAmount { get; set; } = 10f;

    public override void Apply(ICombatant target, HitContext context)
    {
        // Use Blackboard to get HealthComponent
        if (target.Blackboard.TryGet(BBDataSig.HealthComp, out HealthComponent health))
        {
            health.TakeDamage(DamageAmount, context.Source);
        }
    }
}
