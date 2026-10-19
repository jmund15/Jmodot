namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.Combat.Reactions;
using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.AI.BB;

/// <summary>
/// A flexible condition for selecting reaction states based on force-bearing combat events.
/// Matches a <see cref="DamageResult"/> (a hit that both damages and knocks back) by its force, and a
/// <see cref="KnockbackAppliedResult"/> (the push a receiver actually applied, such as a Wind-Blast
/// that delivers impulse without damage) by its velocity change.
/// Enables data-driven reaction tiers (flinch/hurt/launch/crumple) via .tres configuration.
/// A sustained result (<see cref="CombatResult.Sustained"/>) never matches: a held contact's push is
/// not a blow, and re-entering a reaction tier every tick would lock its target in it.
///
/// Example configurations:
/// - Flinch: MinForce=0, MaxForce=15
/// - Launch: MinForce=30, MaxForce=∞
/// - Crumple (high-damage low-knockback hit): pair with DamageCondition's MinDamage/MaxDamage
///
/// MinForce and MaxForce compare the receiver's post-resistance velocity change (m/s) for a
/// KnockbackAppliedResult, and the effect's pre-resistance amplitude for a DamageResult: different
/// physical meanings, so tune thresholds for the result type your hits produce. A producer's own
/// KnockbackResult never matches; only what a receiver applied does.
/// </summary>
[GlobalClass, Tool]
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

    /// <summary>
    /// Evidence window in combat-time seconds. 0 (default) = current physics frame only
    /// (<see cref="CombatLog.HasEvent{T}"/>). Set &gt; 0 when this condition is ANDed with a
    /// persistent physics observation (e.g. VerticalVelocityCondition): the impulse an event
    /// delivers only becomes observable velocity on LATER frames, so a same-frame AND of
    /// event + velocity can never both pass — the window is what makes the composition satisfiable.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,2.0,0.05,suffix:s")]
    public float WithinSeconds { get; set; }

    /// <summary>
    /// On: defers this transition while the victim's push is still held, so a launch-strength hit cannot
    /// match a lower tier first, and after release reads the receiver's held-push evidence window in place
    /// of Within Seconds, a Within Seconds of 0 included. Off: Within Seconds alone decides, and the
    /// transition can fire during the hold.
    /// </summary>
    [Export] public bool DeferWhileHeld { get; set; } = true;

    protected override bool CheckEvent(CombatLog log)
    {
        // Damaging hits route through DamageResult (Fireball, Slap), pushes through the receiver's
        // KnockbackAppliedResult (Wind Blast), logged post-resistance, so launch thresholds match
        // what the actor actually experienced.
        float window = WithinSeconds;
        if (DeferWhileHeld
            && KnockbackComponent3D.TryGetHeldPushWindow(CurrentBlackboard, out var heldWindow, out var holding))
        {
            if (holding) { return false; }
            window = heldWindow;
        }
        return Matches<DamageResult>(log, r => r.Force, window) || Matches<KnockbackAppliedResult>(log, r => r.DeltaV, window);
    }

    private bool Matches<T>(CombatLog log, System.Func<T, float> amount, float window) where T : CombatResult
    {
        bool MatchesEntry(T r)
        {
            if (r.Sustained) { return false; }

            var value = amount(r);
            if (value < MinForce || value > MaxForce)
            {
                return false;
            }

            return CombatTagMatcher.MatchesTags(r.Tags, RequiredTags, TagMatchMode.Any);
        }

        if (window > 0f)
        {
            return log.AnyWithinCombatTime<T>(window, MatchesEntry);
        }
        return log.HasEvent<T>(MatchesEntry);
    }
}
