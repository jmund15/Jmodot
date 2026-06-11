namespace Jmodot.Core.ProcGen.Spatial;

using System;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Spatial port-compatibility predicate: two ports are compatible iff their types match
///     (delegating to <see cref="PortTypes.Matches" /> — the kernel's single type predicate) AND
///     their widths are exactly equal. Throws on a non-<see cref="ISpatialPort" /> input — never a
///     silent type-only fallback. The stage-1 kernel never calls this; only the stage-2 embedder does.
/// </summary>
public static class PortCompatibility
{
    public static bool Matches(IGraphPort a, IGraphPort b)
    {
        ISpatialPort aSpatial = AsSpatial(a, nameof(a));
        ISpatialPort bSpatial = AsSpatial(b, nameof(b));
        return PortTypes.Matches(a.Type, b.Type) && aSpatial.WidthCells == bSpatial.WidthCells;
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
