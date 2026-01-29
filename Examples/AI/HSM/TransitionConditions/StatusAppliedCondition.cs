namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.Combat;
using Core.Combat.Reactions;
using Godot;

/// <summary>
/// A transition condition that checks if a StatusResult with specific tags
/// was logged this physics frame. Used for triggering state transitions
/// when a status effect (like Freeze, Stun) is applied.
/// </summary>
[GlobalClass]
public partial class StatusAppliedCondition : CombatLogCondition
{
    /// <summary>
    /// Optional tags that must be present on the StatusResult's Runner.
    /// If empty, any StatusResult will trigger the condition.
    /// </summary>
    [Export] public Godot.Collections.Array<CombatTag> RequiredTags { get; set; } = [];

    protected override bool CheckEvent(CombatLog log)
    {
        return log.HasEvent<StatusResult>(r =>
        {
            return CombatTagMatcher.MatchesTags(r.Runner?.Tags, RequiredTags, TagMatchMode.Any);
        });
    }
}
