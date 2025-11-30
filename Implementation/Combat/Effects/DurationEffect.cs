using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat.Status;

namespace Jmodot.Implementation.Combat.Effects;

public struct DurationEffect : ICombatEffect
{
    public float Duration;
    public CombatEffectFactory OnStartEffect;
    public CombatEffectFactory OnEndEffect;
    public string[] Tags;

    public event Action<ICombatEffect, bool> EffectCompleted;

    public DurationEffect(float duration, CombatEffectFactory onStartEffect, CombatEffectFactory onEndEffect, string[] tags)
    {
        Duration = duration;
        OnStartEffect = onStartEffect;
        OnEndEffect = onEndEffect;
        Tags = tags;
    }

    public void Apply(ICombatant target, HitContext context)
    {
        if (target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            var runner = new DurationStatusRunner(Duration, OnStartEffect, OnEndEffect);
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
