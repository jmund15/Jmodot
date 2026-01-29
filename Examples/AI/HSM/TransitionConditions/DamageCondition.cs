namespace Jmodot.Examples.AI.HSM.TransitionConditions;

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
    [Export] public float MaxDamage { get; set; } = 1000f;

    /// <summary>
    /// Optional tags that must be present on the damage result.
    /// </summary>
    [Export] public Godot.Collections.Array<CombatTag> RequiredTags { get; set; } = [];

    protected override bool CheckEvent(CombatLog log)
    {
        return log.HasEvent<DamageResult>(r =>
        {
            if (r.FinalAmount < MinDamage || r.FinalAmount > MaxDamage)
            {
                return false;
            }

            return CombatTagMatcher.MatchesTags(r.Tags, RequiredTags, TagMatchMode.Any);
        });
    }
}
