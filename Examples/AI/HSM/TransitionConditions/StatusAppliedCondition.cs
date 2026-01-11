namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using System.Linq;
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
            // If no tags required, any StatusResult matches
            if (RequiredTags == null || RequiredTags.Count == 0)
            {
                return true;
            }

            // Check if the runner has any of the required tags
            if (r.Runner?.Tags == null)
            {
                return false;
            }

            foreach (var reqTag in RequiredTags)
            {
                if (r.Runner.Tags.Any(t => t == reqTag))
                {
                    return true;
                }
            }

            return false;
        });
    }
}
