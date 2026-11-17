namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using System.Collections.Generic;
using System.Linq;
using Core.Combat;
using Core.Combat.Reactions;
using Godot;
using Jmodot.Core.Combat;

/// <summary>
/// Generic combat condition that can filter any CombatResult type by tags,
/// amount thresholds, and force thresholds. Designed to replace or complement
/// specialized conditions (DamageCondition, KnockbackCondition, etc.). A sustained result
/// (<see cref="CombatResult.Sustained"/>) never matches, as in those conditions.
/// </summary>
[GlobalClass, Tool]
public partial class CombatCondition : CombatLogCondition
{
    [Export] public CombatResultType ResultType { get; set; } = CombatResultType.Any;
    [Export] public TagMatchMode TagMode { get; set; } = TagMatchMode.Any;
    [Export] public Godot.Collections.Array<CombatTag> RequiredTags { get; set; } = [];

    [Export] public bool UseAmountThreshold { get; set; } = false;
    [Export] public float MinAmount { get; set; } = 0f;
    [Export] public float MaxAmount { get; set; } = float.MaxValue;

    [Export] public bool UseForceThreshold { get; set; } = false;
    [Export] public float MinForce { get; set; } = 0f;
    [Export] public float MaxForce { get; set; } = float.MaxValue;

    protected override bool CheckEvent(CombatLog log)
    {
        return ResultType switch
        {
            CombatResultType.Damage => log.HasEvent<DamageResult>(r => !r.Sustained && CheckDamageResult(r)),
            CombatResultType.Heal => log.HasEvent<HealResult>(r => !r.Sustained && CheckHealResult(r)),
            CombatResultType.Status => log.HasEvent<StatusResult>(r => !r.Sustained && CheckStatusResult(r)),
            CombatResultType.StatusExpired => log.HasEvent<StatusExpiredResult>(r => !r.Sustained && CheckResult(r)),
            CombatResultType.Stat => log.HasEvent<StatResult>(r => !r.Sustained && CheckResult(r)),
            CombatResultType.Effect => log.HasEvent<EffectResult>(r => !r.Sustained && CheckResult(r)),
            CombatResultType.Any => log.HasEvent<CombatResult>(r => !r.Sustained && r is not ImpactReport && CheckResult(r)),
            _ => false
        };
    }

    private bool CheckResult(CombatResult r)
    {
        return CombatTagMatcher.MatchesTags(r.Tags, RequiredTags, TagMode);
    }

    private bool CheckDamageResult(DamageResult r)
    {
        if (!CombatTagMatcher.MatchesTags(r.Tags, RequiredTags, TagMode))
        {
            return false;
        }

        if (UseAmountThreshold && (r.FinalAmount < MinAmount || r.FinalAmount > MaxAmount))
        {
            return false;
        }

        if (UseForceThreshold && (r.Force < MinForce || r.Force > MaxForce))
        {
            return false;
        }

        return true;
    }

    private bool CheckHealResult(HealResult r)
    {
        if (!CombatTagMatcher.MatchesTags(r.Tags, RequiredTags, TagMode))
        {
            return false;
        }

        if (UseAmountThreshold && (r.AmountHealed < MinAmount || r.AmountHealed > MaxAmount))
        {
            return false;
        }

        // Force threshold is irrelevant for HealResult - skip it
        return true;
    }

    private bool CheckStatusResult(StatusResult r)
    {
        // For StatusResult, check BOTH result.Tags AND Runner.Tags
        IEnumerable<CombatTag> combinedTags = r.Tags;
        if (r.Runner?.Tags != null)
        {
            combinedTags = r.Tags.Concat(r.Runner.Tags);
        }

        return CombatTagMatcher.MatchesTags(combinedTags, RequiredTags, TagMode);
    }
}
