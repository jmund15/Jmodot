using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Status;

namespace Jmodot.Implementation.Combat.Effects;

public struct TickEffect : ICombatEffect
{
    public float Duration;
    public float Interval;
    public CombatEffectFactory EffectToApply;
    public string[] Tags;

    public event Action<ICombatEffect, bool> EffectCompleted;

    public TickEffect(float duration, float interval, CombatEffectFactory effectToApply, string[] tags)
    {
        Duration = duration;
        Interval = interval;
        EffectToApply = effectToApply;
        Tags = tags;
    }

    public void Apply(ICombatant target, HitContext context)
    {
        if (target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            var runner = new TickStatusRunner(Duration, Interval, EffectToApply);
            runner.Initialize(context, target, Tags);
            statusComp.AddStatus(runner);
        }
        
        // Immediate completion as the runner handles the rest
        EffectCompleted?.Invoke(this, true);
    }

    public void Cancel()
    {
        EffectCompleted?.Invoke(this, false);
    }
}
