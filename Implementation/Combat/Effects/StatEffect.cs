namespace Jmodot.Implementation.Combat.Effects;

using System;
using System.Collections;
using System.Collections.Generic;
using AI.BB;
using Core.Combat;
using Core.Combat.Reactions;
using Core.Modifiers;
using Core.Stats;
using Health;
using Shared;
using Attribute = Core.Stats.Attribute;
using Jmodot.Core.Visual.Effects;
using GCol = Godot.Collections;

// public enum RevertableEffectStatus // add in progress statuses?
// {
//     Fresh,
//     Applied,
//     Reverted
// }

/// <summary>
/// Combat Effect that applies a stat modifier
/// </summary>
/// <typeparam name="T"></typeparam>
public struct StatEffect : IRevertibleCombatEffect
{
    public readonly Attribute Attribute;
    public Resource Modifier;
    public ModifierHandle? Handle;
    public IEnumerable<CombatTag> Tags { get; private set; }
    public VisualEffect? Visual { get; private init; }

    public StatEffect(Attribute attribute, Resource modifier, IEnumerable<CombatTag>? tags = null, VisualEffect? visual = null)
    {
        Attribute = attribute;
        Modifier = modifier;
        Tags = tags ?? [];
        Visual = visual;
        Handle = null;
    }

    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        // Use Blackboard
        if (!target.Blackboard.TryGet<StatController>(BBDataSig.Stats, out var stats))
        {
            JmoLogger.Warning(this, $"Target '{target.GetUnderlyingNode().Name}' has no StatController!");
            return null;
        }
        if (!stats!.TryAddModifier(Attribute, Modifier, this, out var handle))
        {
            JmoLogger.Warning(this, $"StatEffect was unable to apply stat modification!");
            return null;
        }
        Handle = handle!;
        return new StatResult()
        {
            Source = context.Source,
            Target = target.OwnerNode,
            Tags = Tags,
            AttributeAffected = Attribute
        };
    }
    public ICombatEffect? GetRevertEffect(ICombatant target, HitContext context)
    {
        if (Handle == null)
        {
            JmoLogger.Warning(this, $"Can't get revert effect — stat effect wasn't applied.");
            return null;
        }
        if (!target.Blackboard.TryGet<StatController>(BBDataSig.Stats, out var stats))
        {
            JmoLogger.Warning(this, $"Target '{target.GetUnderlyingNode().Name}' has no StatController!");
            return null;
        }
        return new RevertStatEffect(Handle, Tags);
    }
}
