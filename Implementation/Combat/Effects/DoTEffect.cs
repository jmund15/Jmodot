using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Status;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat.Effects;

using System;

public struct DoTEffect(float duration, float tickInterval, float damagePerTick) : ICombatEffect
{
    public float Duration = duration;
    public float TickInterval = tickInterval;
    public float DamagePerTick = damagePerTick;

    public void Apply(ICombatant target, HitContext context)
    {
        if (target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            var status = new DoTStatus(Duration, TickInterval, DamagePerTick, context.Source, target);
            statusComp.AddStatus(status);
        }
    }

    public void Cancel()
    {
        EffectCompleted?.Invoke(this);
    }

    public event Action<ICombatEffect>? EffectCompleted;
}
