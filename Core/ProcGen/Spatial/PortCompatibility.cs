namespace Jmodot.Core.ProcGen.Spatial;

using System;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Spatial port-compatibility predicate: two ports are compatible iff their types match
///     (delegating to <see cref="PortTypes.Matches" /> — the kernel's single type predicate) AND
///     their door-width ranges <c>[MinWidthCells, WidthCells]</c> intersect. Throws on a non-<see cref="ISpatialPort" /> input — never a
///     silent type-only fallback. The stage-1 kernel never calls this; only the stage-2 embedder does.
/// </summary>
public static class PortCompatibility
{
    public static bool Matches(IGraphPort a, IGraphPort b)
    {
        ISpatialPort aSpatial = AsSpatial(a, nameof(a));
        ISpatialPort bSpatial = AsSpatial(b, nameof(b));
        return PortTypes.Matches(a.Type, b.Type) &&
               CommonWidthRange(aSpatial.MinWidthCells, aSpatial.WidthCells, bSpatial.MinWidthCells, bSpatial.WidthCells) != null;
    }

    /// <summary>
    ///     The door widths two ports both admit: from the larger of the two minimums to the smaller of the two widest
    ///     doors. Null when the ranges do not intersect. A returned range can still start below one when a port
    ///     reports no minimum, so a caller that places a door checks <c>Min &gt;= 1</c> itself.
    /// </summary>
    public static (int Min, int Max)? CommonWidthRange(int minA, int widestA, int minB, int widestB)
    {
        int min = Math.Max(minA, minB);
        int max = Math.Min(widestA, widestB);
        return min <= max ? (min, max) : null;
    }

    private static ISpatialPort AsSpatial(IGraphPort port, string paramName)
    {
        if (port is ISpatialPort spatial)
        {
            return spatial;
        }

        throw new ArgumentException(
            $"PortCompatibility requires both ports to implement ISpatialPort; '{paramName}' ({port?.GetType().Name ?? "null"}) does not.",
            paramName);
    }
}
