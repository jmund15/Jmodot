using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Status;

namespace Jmodot.Implementation.Combat.Effects;

public struct ConditionEffect : ICombatEffect
{
    public StatusCondition Condition;
    public float CheckInterval;
    public CombatEffectFactory EffectToApplyOnTick;
    public CombatEffectFactory EffectToApplyOnEnd;
    public string[] Tags;

    public event Action<ICombatEffect, bool> EffectCompleted;

    public ConditionEffect(StatusCondition condition, float checkInterval, CombatEffectFactory effectToApplyOnTick, CombatEffectFactory effectToApplyOnEnd, string[] tags)
    {
        Condition = condition;
        CheckInterval = checkInterval;
        EffectToApplyOnTick = effectToApplyOnTick;
        EffectToApplyOnEnd = effectToApplyOnEnd;
        Tags = tags;
    }

    public void Apply(ICombatant target, HitContext context)
    {
        if (target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            var runner = new ConditionStatusRunner(Condition, CheckInterval, EffectToApplyOnTick, EffectToApplyOnEnd);
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
