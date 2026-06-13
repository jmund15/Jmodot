namespace Jmodot.Core.Combat;

/// <summary>
/// The lineage seed handed to <see cref="ICombatEffectFactory.Create"/> at effect-assembly time.
/// A null <c>EffectCreationSeed?</c> argument maps to <see cref="SeedProvenance.Missing"/> — the
/// optional-parameter shape lets the existing factory implementers stay unforced. L4 only carries
/// this through the pipeline; L5 consumers (crit, etc.) read <see cref="Seed"/> to derive rolls.
/// </summary>
public readonly record struct EffectCreationSeed(int Seed);
