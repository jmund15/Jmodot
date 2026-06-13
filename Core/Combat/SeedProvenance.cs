namespace Jmodot.Core.Combat;

/// <summary>
/// Provenance of an attack payload's lineage seed. Distinguishes a genuinely seeded attack
/// from one that is deliberately unseeded versus one whose seed is missing by error. The
/// receiver-side hit-seed derivation gates on this (the 2×2 table): <see cref="Seeded"/>
/// derives a hit seed, <see cref="UnseededByDesign"/> skips silently, <see cref="Missing"/>
/// skips with a warning. Carriers only in L4; consumed by the crit/knockback/status path in L5.
/// </summary>
public enum SeedProvenance
{
    /// <summary>The attack carries a real lineage seed; derive a hit seed from it.</summary>
    Seeded,

    /// <summary>
    /// Deliberately unseeded — e.g. a spell-cast roll that stays non-deterministic until the
    /// future cast-lineage Part lands. Receiver skips hit-seed derivation SILENTLY (no warning).
    /// </summary>
    UnseededByDesign,

    /// <summary>
    /// A seed was expected but is absent (a wiring gap). Receiver skips derivation and WARNS.
    /// The default for the null <c>EffectCreationSeed?</c> path.
    /// </summary>
    Missing,
}
