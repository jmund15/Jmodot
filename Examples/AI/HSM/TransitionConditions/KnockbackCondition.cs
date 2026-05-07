namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.Combat.Reactions;
using Godot;
using Jmodot.Core.Combat;

/// <summary>
/// A flexible condition for selecting reaction states based on force-bearing combat events.
/// Matches any <see cref="CombatResult"/> implementing <see cref="IForceCarrier"/> — currently
/// <see cref="DamageResult"/> (e.g., a hit that both damages and knocks back) and
/// <see cref="KnockbackResult"/> (e.g., a Wind-Blast push that delivers impulse without damage).
/// Enables data-driven reaction tiers (flinch/hurt/launch/crumple) via .tres configuration.
///
/// Example configurations:
/// - Flinch: MinForce=0, MaxForce=15
/// - Launch: MinForce=30, MaxForce=∞
/// - Crumple (high-damage low-knockback hit): pair with DamageCondition's MinDamage/MaxDamage
///
/// The Force value matched here is the receiver's post-resistance velocity-delta (m/s) for
/// KnockbackResult, or the effect's pre-resistance amplitude for DamageResult — both are
/// IForceCarrier.Force, but with different physical interpretation. Tune thresholds per
/// the result type your effects emit.
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
        // OR-combine across both force-bearing CombatResult subtypes. Damaging hits route
        // through DamageResult (Fireball, Slap), pure-impulse hits through KnockbackResult
        // (Wind Blast). The receiver-side KnockbackComponent3D writes KnockbackResult
        // post-resistance, so launch thresholds match what the actor actually experienced.
        return MatchesForceCarrier<DamageResult>(log) || MatchesForceCarrier<KnockbackResult>(log);
    }

    private bool MatchesForceCarrier<T>(CombatLog log) where T : CombatResult, IForceCarrier
    {
        return log.HasEvent<T>(r =>
        {
            if (r.Force < MinForce || r.Force > MaxForce)
            {
                return false;
            }

            return CombatTagMatcher.MatchesTags(r.Tags, RequiredTags, TagMatchMode.Any);
        });
    }
}
