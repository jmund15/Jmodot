namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.Combat;
using Core.Combat.Reactions;
using Jmodot.Core.Shared.Attributes;

[GlobalClass]
public partial class CombatResultTagCondition : CombatLogCondition
{
    [Export, RequiredExport] public Godot.Collections.Array<CombatTag> RequiredTags { get; set; } = null!;

    protected override bool CheckEvent(CombatLog log)
    {
        return log.HasEvent<CombatResult>(r =>
        {
            return CombatTagMatcher.MatchesTags(r.Tags, RequiredTags, TagMatchMode.Any);
        });
    }
}
