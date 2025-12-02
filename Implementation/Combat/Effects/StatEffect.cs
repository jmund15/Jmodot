namespace Jmodot.Implementation.Combat.Effects;

using System;
using System.Collections;
using System.Collections.Generic;
using AI.BB;
using Core.Combat;
using Core.Modifiers;
using Core.Stats;
using Health;
using Shared;
using Attribute = Core.Stats.Attribute;
using GCol = Godot.Collections;

public enum RevertableEffectStatus // add in progress statuses?
{
    Fresh,
    Applied,
    Reverted
}

/// <summary>
/// Combat Effect that applies a stat modifier
/// </summary>
/// <typeparam name="T"></typeparam>
public struct StatEffect : IRevertibleCombatEffect
{
    public readonly Attribute Attribute;
    public Resource Modifier;
    public ModifierHandle? Handle;
    public RevertableEffectStatus Status;
    public IEnumerable<GameplayTag> Tags { get; private set; }

    public StatEffect(Attribute attribute, Resource modifier, IEnumerable<GameplayTag>? tags = null)
    {
        Attribute = attribute;
        Modifier = modifier;
        Tags = tags ?? [];
        Handle = null;
        Status =  RevertableEffectStatus.Fresh;
    }

    public void Apply(ICombatant target, HitContext context)
    {
        if (Status == RevertableEffectStatus.Applied)
        {
            JmoLogger.Error(this, $"Can not apply StatEffect as it has already been applied!");
            return;
        }
        // Use Blackboard
        if (!target.Blackboard.TryGet<StatController>(BBDataSig.Stats, out var stats))
        {
            JmoLogger.Error(this, $"Target '{target.GetUnderlyingNode().Name}' has no HealthComponent!");
            return;
        }
        if (!stats!.TryAddModifier(Attribute, Modifier, this, out var handle))
        {
            JmoLogger.Error(this, $"StatEffect was unable to apply stat modification!");
            return;
        }
        Handle = handle!;
        EffectCompleted?.Invoke(this, true);
        Status =  RevertableEffectStatus.Applied;
    }

    public void Cancel()
    {
        if (Status == RevertableEffectStatus.Fresh)
        {
            EffectCompleted?.Invoke(this, false);
        }
        // otherwise what?? honestly i have no idea what this function should do haha
    }

    public bool TryRevert(ICombatant target, HitContext context)
    {
        if (Status != RevertableEffectStatus.Applied)
        {
            return false;
        }
        if (!target.Blackboard.TryGet<StatController>(BBDataSig.Stats, out var stats))
        {
            JmoLogger.Error(this, $"Target '{target.GetUnderlyingNode().Name}' has no HealthComponent!");
            return false;
        }

        stats!.RemoveModifier(Handle!);
        EffectReverted?.Invoke(this);
        Status = RevertableEffectStatus.Reverted;
        return true;
    }

    public event Action<ICombatEffect, bool> EffectCompleted;
    public event Action<IRevertibleCombatEffect> EffectReverted;
}
