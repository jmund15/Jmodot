namespace Jmodot.Core.Shared;

using System;

/// <summary>
///     An inclusive integer interval <c>[Min, Max]</c>. A dependency-free value type for
///     deterministic generation config (counts, spans, budgets). Equality is structural
///     (record struct), so two ranges with the same bounds compare equal.
/// </summary>
public readonly record struct IntRange(int Min, int Max)
{
    /// <summary>True if <paramref name="value" /> lies within <c>[Min, Max]</c> (both ends inclusive).</summary>
    public bool Contains(int value) => value >= this.Min && value <= this.Max;

    /// <summary>
    ///     Fail-fast guard: throws <see cref="ArgumentException" /> if <see cref="Min" /> exceeds
    ///     <see cref="Max" />. A degenerate single-value range (<c>Min == Max</c>) is valid.
    /// </summary>
    public void Validate()
    {
        if (this.Min > this.Max)
        {
            throw new ArgumentException($"IntRange Min ({this.Min}) must not exceed Max ({this.Max}).");
        }
    }
}
