namespace Jmodot.Implementation.Body.Segmented;

using System;
using System.Collections.Generic;

/// <summary>
/// Turns "these units of a body died this frame" into the contiguous runs that survived it, in
/// front-to-back order. Pure: it knows nothing of nodes, poses or promotion, and one call answers a
/// whole frame's worth of deaths rather than one death at a time.
/// </summary>
public static class SegmentSplitResolver
{
    /// <summary>
    /// The surviving runs of unit indices, front first, each half-open <c>[Start, End)</c> over the
    /// roster the caller resolved against. Empty runs are never emitted, so the head keeps a body
    /// exactly when the first returned run starts at index 0.
    /// </summary>
    /// <param name="deadIndices">Indices of the units that died, all within the roster.</param>
    /// <param name="segmentCount">Size of the roster the indices address.</param>
    /// <param name="minLength">
    /// Shortest total body — head included — that stays alive. Validated here so every caller shares
    /// one precondition; classifying each returned run against it is the caller's own step, because
    /// the head's run and a severed tail spend it differently.
    /// </param>
    /// <returns>
    /// Runs whose lengths, summed with <paramref name="deadIndices"/>, always equal
    /// <paramref name="segmentCount"/> — checked before returning, so a unit can never go missing
    /// silently between the roster and the split.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="segmentCount"/> is negative, <paramref name="minLength"/> is below two, or an
    /// index in <paramref name="deadIndices"/> falls outside the roster.
    /// </exception>
    /// <exception cref="InvalidOperationException">The resolved runs do not account for every unit.</exception>
    public static IReadOnlyList<Range> Resolve(IReadOnlySet<int> deadIndices, int segmentCount, int minLength)
    {
        ArgumentNullException.ThrowIfNull(deadIndices);
        if (segmentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentCount), segmentCount, "A roster cannot have negative size.");
        }

        if (minLength < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(minLength), minLength,
                "A body shorter than a head plus one unit is just the head.");
        }

        foreach (var index in deadIndices)
        {
            if (index < 0 || index >= segmentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(deadIndices), index,
                    "A dead index addresses no unit in the roster it was resolved against.");
            }
        }

        var fragments = new List<Range>();
        var runStart = -1;
        for (var i = 0; i < segmentCount; i++)
        {
            if (!deadIndices.Contains(i))
            {
                if (runStart < 0) { runStart = i; }
                continue;
            }

            if (runStart >= 0) { fragments.Add(new Range(runStart, i)); }
            runStart = -1;
        }

        if (runStart >= 0) { fragments.Add(new Range(runStart, segmentCount)); }

        var surviving = 0;
        foreach (var fragment in fragments) { surviving += fragment.End.Value - fragment.Start.Value; }

        if (surviving + deadIndices.Count != segmentCount)
        {
            throw new InvalidOperationException(
                $"Split resolution lost units: {surviving} surviving plus {deadIndices.Count} dead is not {segmentCount}.");
        }

        return fragments;
    }
}
