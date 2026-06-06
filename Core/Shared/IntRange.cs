namespace Jmodot.Core.Shared;

using System;
using Godot;

/// <summary>
///     An inclusive integer interval <c>[Min, Max]</c> for deterministic generation config
///     (counts, spans, budgets). A <see cref="Resource" /> so designers author it once in the
///     Inspector and share it across config Resources (depth ranges, spine/branch specs, budgets)
///     instead of repeating int-pair <c>[Export]</c>s. Reference identity, not value equality —
///     two ranges with the same bounds are distinct instances.
/// </summary>
[GlobalClass, Tool]
public sealed partial class IntRange : Resource
{
    /// <summary>Inclusive lower bound. Defaults to 0; consumers decide what an unset/zero bound means.</summary>
    [Export] public int Min { get; set; }

    /// <summary>Inclusive upper bound. Defaults to 0; consumers decide what an unset/zero bound means.</summary>
    [Export] public int Max { get; set; }

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
