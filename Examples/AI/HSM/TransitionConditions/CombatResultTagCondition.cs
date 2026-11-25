namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Jmodot.Core.AI.BB;
using Core.Combat;
using Core.Combat.Reactions;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// Passes on any result in the current frame's combat log carrying a required tag. A sustained result
/// (<see cref="CombatResult.Sustained"/>) never passes, as in <see cref="DamageCondition"/>.
/// </summary>
[GlobalClass, Tool]
public partial class CombatResultTagCondition : CombatLogCondition
{
    [Export, RequiredExport] public Godot.Collections.Array<CombatTag> RequiredTags { get; set; } = null!;

    protected override bool CheckEvent(CombatLog log, IBlackboard bb)
    {
        return log.HasEvent<CombatResult>(r =>
        {
            return !r.Sustained && r is not ImpactReport && CombatTagMatcher.MatchesTags(r.Tags, RequiredTags, TagMatchMode.Any);
        });
    }
}
