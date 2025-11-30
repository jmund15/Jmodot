using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Status;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat.Effects;

using System;

public struct StunEffect(float duration) : ICombatEffect
{
    public float Duration = duration;

    public void Apply(ICombatant target, HitContext context)
    {
        if (target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            var status = new StunStatus(Duration, context.Source, target);
            statusComp.AddStatus(status);
        }
    }
    public void Cancel()
    {
        EffectCompleted?.Invoke(this);
    }
    public event Action<ICombatEffect>? EffectCompleted;
}
