namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using System.Linq;
using Core.Combat.Reactions;
using Godot;
using Jmodot.Core.Combat;

[GlobalClass]
public partial class DamageCondition : CombatLogCondition
{
    /// <summary>
    /// Minimum damage amount (FinalAmount) required to trigger this condition.
    /// </summary>
    [Export] public float MinDamage { get; set; } = 0f;

    /// <summary>
    /// Maximum damage amount (FinalAmount) allowed for this condition.
    /// Use a very high number for no upper limit.
    /// </summary>
    [Export] public float MaxDamage { get; set; } = 100f;

    /// <summary>
    /// Optional tags that must be present on the damage result.
    /// </summary>
    [Export] public Godot.Collections.Array<CombatTag> RequiredTags { get; set; } = [];

    protected override bool CheckEvent(CombatLog log)
    {
        return log.HasEvent<DamageResult>(r =>
        {
            // 1. Damage Threshold Check
            if (r.FinalAmount < MinDamage || r.FinalAmount > MaxDamage)
            {
                return false;
            }

            // 2. Tag Check
            if (RequiredTags != null && RequiredTags.Count > 0)
            {
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
