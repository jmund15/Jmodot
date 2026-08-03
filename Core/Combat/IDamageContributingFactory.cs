namespace Jmodot.Core.Combat;

using Stats;

/// <summary>
/// Capability query: this factory's base damage participates in a hitbox's SINGLE-ROLL damage fold.
/// <para>
/// When a hitbox assembles a swing, every marked factory delivered through its
/// <see cref="IEffectFactorySource"/> slot has its <see cref="ResolveBaseDamageContribution"/>
/// summed into the fold target's base BEFORE any effect is constructed. The fold target — the first
/// marked factory in the hitbox's authored <c>DefaultEffects</c> — then builds ONE damage effect via
/// <see cref="CreateWithComposedBase"/>, so exactly one crit roll exists and it covers the composed
/// total under both crit-resolution modes. Contributed factories produce no effect object at all.
/// </para>
/// <para>
/// A folded contribution contributes its BASE DAMAGE ONLY. Its tags, knockback, spatial falloff and
/// visual do NOT merge into the fold target — those belong to the swing's own damage effect. A
/// contributor that wants its own tags/visual/knockback supplies a SEPARATE non-damage factory
/// (status/tick/knockback), which is added normally and untouched by the fold.
/// </para>
/// <para>
/// Authoring rule for new damage-shaped factories: implement this interface so your damage folds.
/// The hitbox's loud rejection is NARROWER than that rule — it fires only when a slot factory's
/// <c>Create</c> RETURNS a <c>DamageEffect</c>. Damage carried through any other shape is NOT
/// detectable by the framework, does NOT fold, and carries its own crit roll: a derived effect type
/// (<c>DistanceScaledDamageEffect</c>) or a wrapper factory holding a damage factory
/// (<c>DelayedEffectFactory</c>, <c>TickEffectFactory</c>, <c>DurationRevertibleEffectFactory</c>).
/// Rejecting those would require a closed-set type test over an open factory family, so the policy
/// is owned by authoring-time validation on the game-layer composite source, not by the hitbox.
/// A factory whose damage is modulated per-target at apply time (distance scaling) must NOT
/// implement this interface — a flat pre-roll base folded into it would be silently rescaled per target.
/// </para>
/// </summary>
public interface IDamageContributingFactory
{
    /// <summary>This factory's PRE-crit base damage, which is NON-NEGATIVE by contract (the hitbox
    /// clamps at zero — a contribution never subtracts from the swing's base). Must be pure: the
    /// hitbox may resolve it more than once per swing and assumes no observable effect.</summary>
    float ResolveBaseDamageContribution(IStatProvider? stats);

    /// <summary>
    /// Build this factory's damage effect over <paramref name="composedBase"/> — a base that already
    /// includes sibling contributions. <c>Create(stats, seed)</c> is exactly
    /// <c>CreateWithComposedBase(stats, seed, ResolveBaseDamageContribution(stats))</c>, so the
    /// un-augmented path is an algebraic identity rather than a second branch.
    /// </summary>
    ICombatEffect CreateWithComposedBase(IStatProvider? stats, EffectCreationSeed? seed, float composedBase);
}
