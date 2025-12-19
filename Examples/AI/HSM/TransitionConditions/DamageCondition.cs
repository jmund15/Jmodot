namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using System.Linq;
using Core.Combat.Reactions;
using Godot;
using Jmodot.Core.Combat;

[GlobalClass]
public partial class DamageCondition : CombatLogCondition
{
    [Export] public float MinAmount { get; set; } = 0f;
    [Export] public Godot.Collections.Array<CombatTag> RequiredTags { get; set; }

    protected override bool CheckEvent(CombatLog log)
    {
        // We use the Generic method directly. No casting, no switching.
        return log.HasEvent<DamageResult>(r =>
        {
            // 1. Amount Check
            if (r.FinalAmount < MinAmount) return false;

            // 2. Tag Check
            if (RequiredTags != null && RequiredTags.Count > 0)
            {
                // Example: Requires ANY of the tags (e.g., "Fire" OR "Explosion")
                // You can change this to ALL if you prefer strict matching.
                bool hasTag = false;
                foreach (var reqTag in RequiredTags)
                {
                    // Assuming CombatResult.Tags is IEnumerable<GameplayTag>
                    foreach (var resTag in r.Tags)
                    {
                        if (resTag == reqTag)
                        {
                            hasTag = true;
                            break;
                        }
                    }
                    if (hasTag) break;
                }
                //GD.Print($"HAS REQ TAG?? : {hasTag}");
                if (!hasTag) return false;
            }

            return true;
        });
    }
}
