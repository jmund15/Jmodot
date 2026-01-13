namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.Combat.Reactions;
using Godot;
using Jmodot.Core.Combat;

/// <summary>
/// A flexible condition for selecting reaction states based on damage and knockback thresholds.
/// Enables data-driven reaction tiers (flinch/hurt/launch/crumple) via .tres configuration.
///
/// Example configurations:
/// - Flinch: MinForce=0, MaxForce=15, MinDamage=0, MaxDamage=20
/// - Launch: MinForce=30, MaxForce=∞, MinDamage=0, MaxDamage=∞
/// - Crumple: MinForce=0, MaxForce=15, MinDamage=50, MaxDamage=∞
/// </summary>
[GlobalClass]
public partial class KnockbackCondition : CombatLogCondition
{
    /// <summary>
    /// Minimum knockback force required to trigger this condition.
    /// </summary>
    [Export] public float MinForce { get; set; } = 0f;

    /// <summary>
    /// Maximum knockback force allowed for this condition.
    /// Use float.MaxValue (or a very high number in editor) for no upper limit.
    /// </summary>
    [Export] public float MaxForce { get; set; } = float.MaxValue;

    /// <summary>
    /// Optional tags that must be present on the damage result.
    /// If empty, no tag filtering is applied.
    /// </summary>
    [Export] public Godot.Collections.Array<CombatTag> RequiredTags { get; set; } = [];

    protected override bool CheckEvent(CombatLog log)
    {
        return log.HasEvent<DamageResult>(r =>
        {
            // 1. Force Threshold Check
            if (r.Force < MinForce || r.Force > MaxForce)
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
