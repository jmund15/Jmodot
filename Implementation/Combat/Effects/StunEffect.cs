using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Status;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat.Effects;

[GlobalClass]
public partial class StunEffect : CombatEffect
{
    [Export]
    public float Duration { get; set; } = 2f;

    public override void Apply(ICombatant target, HitContext context)
    {
        if (target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            var status = new StunStatus(Duration, context.Source, target);
            statusComp.AddStatus(status);
        }
    }
}
