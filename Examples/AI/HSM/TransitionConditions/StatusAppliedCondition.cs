namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using System.Collections.Generic;
using System.Linq;
using Core.Combat;
using Core.Combat.Reactions;
using Godot;
using Jmodot.Implementation.Shared;

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
        // DIAGNOSTIC: Log what we're checking for
        var requiredTagIds = RequiredTags?.Select(t => t?.TagId.ToString() ?? "null").ToList() ?? new List<string>();
        JmoLogger.Info(this, $"[DIAG] StatusAppliedCondition checking for tags: [{string.Join(", ", requiredTagIds)}]");

        // DIAGNOSTIC: Get all StatusResults this frame for logging
        var statusResults = log.GetEvents<StatusResult>().ToList();
        JmoLogger.Info(this, $"[DIAG] Found {statusResults.Count} StatusResult(s) in CombatLog this frame");

        foreach (var sr in statusResults)
        {
            var runnerTagIds = sr.Runner?.Tags?.Select(t => t?.TagId.ToString() ?? "null").ToList() ?? new List<string>();
            JmoLogger.Info(this, $"[DIAG]   StatusResult runner tags: [{string.Join(", ", runnerTagIds)}]");
        }

        var result = log.HasEvent<StatusResult>(r =>
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
                    JmoLogger.Info(this, $"[DIAG] TAG MATCH FOUND: {reqTag?.TagId}");
                    return true;
                }
            }

            return false;
        });

        JmoLogger.Info(this, $"[DIAG] StatusAppliedCondition result: {result}");
        return result;
    }
}
