using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Status;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat.Effects;

[GlobalClass]
public partial class DoTEffect : CombatEffect
{
    [Export]
    public float Duration { get; set; } = 3f;

    [Export]
    public float TickInterval { get; set; } = 1f;

    [Export]
    public float DamagePerTick { get; set; } = 5f;

    public override void Apply(ICombatant target, HitContext context)
    {
        if (target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            var status = new DoTStatus(Duration, TickInterval, DamagePerTick, context.Source, target);
            statusComp.AddStatus(status);
        }
    }
}
