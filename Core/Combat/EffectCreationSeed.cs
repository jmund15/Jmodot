namespace Jmodot.Core.Combat;

/// <summary>
/// The lineage seed handed to <see cref="ICombatEffectFactory.Create"/> at effect-assembly time.
/// A null <c>EffectCreationSeed?</c> argument maps to <see cref="SeedProvenance.Missing"/> — the
/// optional-parameter shape lets the existing factory implementers stay unforced.
/// <para>
/// <see cref="Resolution"/> rides alongside the seed to tell a damage factory whether to roll crit now
/// or defer it per-hit. The meaning of <see cref="Seed"/> follows <see cref="Resolution"/>:
/// for <see cref="CritResolution.Resolved"/> it is the pre-derived crit-roll seed
/// (<c>DeriveChild(attackSeed,"crit",effectIdx)</c>); for <see cref="CritResolution.DeferredPerHit"/>
/// it carries the <c>effectIdx</c> the effect folds with the per-hit <c>HitSeed</c> at apply time.
/// The <see cref="CritResolution.Resolved"/> default keeps every L4-era <c>new EffectCreationSeed(seed)</c>
/// construction meaning "resolve at creation".
/// </para>
/// </summary>
public readonly record struct EffectCreationSeed(int Seed, CritResolution Resolution = CritResolution.Resolved);
