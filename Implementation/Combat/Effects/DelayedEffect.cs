using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Status;

namespace Jmodot.Implementation.Combat.Effects;

public struct DelayedEffect : ICombatEffect
{
    public float Delay;
    public CombatEffectFactory EffectToApply;
    public string[] Tags;

    public event Action<ICombatEffect, bool> EffectCompleted;

    public DelayedEffect(float delay, CombatEffectFactory effectToApply, string[] tags)
    {
        Delay = delay;
        EffectToApply = effectToApply;
        Tags = tags;
    }

    public void Apply(ICombatant target, HitContext context)
    {
        if (target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            var runner = new DelayedStatusRunner(Delay, EffectToApply);
            runner.Initialize(context, target, Tags);
            statusComp.AddStatus(runner);
        }
        
        EffectCompleted?.Invoke(this, true);
    }

    public void Cancel()
    {
        EffectCompleted?.Invoke(this, false);
    }
}
