namespace Jmodot.Core.Shared;

using System;
using System.Collections.Generic;

/// <summary>
///     Deterministic weighted selection over a list of <c>(Item, Weight)</c> pairs. Fully
///     data-driven: both the weights and the randomness (the <c>roll</c>) are passed as data, so
///     the primitive is pure — no injected delegates, no RNG dependency. A caller produces the
///     roll with its own seeded RNG (e.g. <c>JmoRng.GetRndLong(WeightedPick.TotalWeight(choices))</c>),
///     keeping this surface free of any Godot runtime coupling.
///     <para>
///     Weights are <c>long</c> by design: a determinism guardrail. <c>float</c>/<c>double</c> weights
///     would break cross-platform determinism via summation rounding, and <c>int</c> overflows when
///     weight products grow. The selection is an ordered-index cumulative walk — input order is
///     preserved and never sorted.
///     </para>
/// </summary>
public static class WeightedPick
{
    /// <summary>
    ///     Sum of all weights — the authoritative exclusive upper bound for a valid <c>roll</c>
    ///     passed to <see cref="Pick{T}" />. Does not validate; <see cref="Pick{T}" /> owns the guards.
    /// </summary>
    public static long TotalWeight<T>(IReadOnlyList<(T Item, long Weight)> choices)
    {
        if (choices == null)
        {
            throw new ArgumentNullException(nameof(choices));
        }

        long total = 0;
        for (int i = 0; i < choices.Count; i++)
        {
            total += choices[i].Weight;
        }
        return total;
    }

    /// <summary>
    ///     Selects the item whose cumulative-weight bucket contains <paramref name="roll" />. Buckets
    ///     are half-open <c>[lo, hi)</c> in input order, so a zero-weight choice occupies an empty
    ///     bucket and is never selected. Fail-fast: empty/all-zero choice sets and out-of-range rolls
    ///     are bugs in the caller, not silent <c>default</c> returns.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="choices" /> is empty or all weights are zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Any weight is negative, or <paramref name="roll" /> ∉ [0, TotalWeight).</exception>
    public static T Pick<T>(IReadOnlyList<(T Item, long Weight)> choices, long roll)
    {
        if (choices == null)
        {
            throw new ArgumentNullException(nameof(choices));
        }
        if (choices.Count == 0)
        {
            throw new ArgumentException("choices must not be empty.", nameof(choices));
        }

        long total = 0;
        for (int i = 0; i < choices.Count; i++)
        {
            long weight = choices[i].Weight;
            if (weight < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(choices), weight, "weights must be non-negative.");
            }
            total += weight;
        }

        if (total == 0)
        {
            throw new ArgumentException("total weight must be positive.", nameof(choices));
        }
        if (roll < 0 || roll >= total)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roll), roll, $"roll must lie in [0, {total}).");
        }

        long cumulative = 0;
        for (int i = 0; i < choices.Count; i++)
        {
            cumulative += choices[i].Weight;
            if (roll < cumulative)
            {
                return choices[i].Item;
            }
        }

        // Unreachable: roll < total guarantees a bucket was found above.
        throw new InvalidOperationException("WeightedPick walk failed to select a choice.");
    }
}
