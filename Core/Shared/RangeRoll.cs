namespace Jmodot.Core.Shared;

/// <summary>
///     Deterministic uniform sampler over an inclusive <see cref="IntRange" />. Fully data-driven:
///     the randomness (the <paramref name="roll" />) is passed as data, so the primitive is pure —
///     no injected delegate, no RNG dependency, no Godot runtime coupling. A caller produces the
///     roll with its own seeded RNG (e.g. <c>rng.GetRndLong(range.Max - range.Min + 1)</c>),
///     mirroring the <see cref="WeightedPick" /> data-param precedent in this folder and keeping
///     <c>Core/Shared</c> independent of <c>Implementation/Shared/JmoRng</c>.
/// </summary>
public static class RangeRoll
{
    /// <summary>
    ///     Maps <paramref name="roll" /> ∈ <c>[0, Max-Min+1)</c> onto the inclusive interval
    ///     <c>[Min, Max]</c> as <c>Min + roll</c>. <c>roll == 0</c> yields <see cref="IntRange.Min" />;
    ///     <c>roll == Max-Min</c> yields <see cref="IntRange.Max" />. The caller owns the roll's range
    ///     guard (parallel to <see cref="WeightedPick.Pick{T}" />, where the caller derives the bound
    ///     from <see cref="WeightedPick.TotalWeight{T}" />).
    /// </summary>
    public static int Within(in IntRange r, long roll) => r.Min + (int)roll;
}
