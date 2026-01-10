namespace Jmodot.Implementation.Combat.Effects;

using System.Collections.Generic;
using System.Reflection.Metadata;
using AI.BB;
using Core.Combat;
using Core.Combat.Reactions;
using Core.Modifiers;
using Core.Stats;
using Shared;
using Jmodot.Core.Visual.Effects;

/// <summary>
/// Combat Effect that applies a stat modifier
/// </summary>
/// <typeparam name="T"></typeparam>
public struct RevertStatEffect : ICombatEffect
{
    public readonly ModifierHandle ModifierToRevert;
    public IEnumerable<CombatTag> Tags { get; private set; }
    public VisualEffect? Visual { get; private init; }

    public RevertStatEffect(ModifierHandle modifierToRevert, IEnumerable<CombatTag>? tags = null)
    {
        ModifierToRevert = modifierToRevert;
        Tags = tags ?? [];
    }

    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        // Use Blackboard
        if (!target.Blackboard.TryGet<StatController>(BBDataSig.Stats, out var stats))
        {
            JmoLogger.Error(this, $"Target '{target.GetUnderlyingNode().Name}' has no HealthComponent!");
            return null;
        }

        stats!.RemoveModifier(ModifierToRevert);
        return new StatResult()
        {
            Source = context.Source,
            Tags = Tags,
            Target = target.OwnerNode
        };
    }
}
