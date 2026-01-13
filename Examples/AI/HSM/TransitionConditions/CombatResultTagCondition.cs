namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.Combat;
using Core.Combat.Reactions;

[GlobalClass]
public partial class CombatResultTagCondition : CombatLogCondition
{
    [Export] public Godot.Collections.Array<CombatTag> RequiredTags { get; set; } = null!;

    protected override bool CheckEvent(CombatLog log)
    {
        return log.HasEvent<CombatResult>(r =>
        {
            // 1. Tag Check
            if (RequiredTags.Count > 0)
            {
                // Example: Requires ANY of the tags (e.g., "Fire" OR "Explosion")
                // You can change this to ALL if you prefer strict matching.
                bool hasTag = false;
                foreach (var reqTag in RequiredTags)
                {
                    foreach (var resTag in r.Tags)
                    {
                        if (resTag == reqTag)
                        {
                            hasTag = true;
                            break;
                        }
                    }
                    if (hasTag) { break; }
                }
                if (!hasTag) { return false; }
            }

            return true;
        });
    }
}
