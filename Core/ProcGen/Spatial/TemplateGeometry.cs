namespace Jmodot.Core.ProcGen.Spatial;

using System.Collections.Generic;
using Godot;

/// <summary>
///     Template-local, pose-free geometry over a node template's ports. Lives beside <see cref="SpatialParity" /> so
///     the stage-1 generator reads template geometry without depending on the stage-2 pose math.
/// </summary>
public static class TemplateGeometry
{
    /// <summary>
    ///     The mean Manhattan distance, in cells, between the centres of the template's distinct port spans, rounded
    ///     to the nearest cell with halves rounding up. A span's centre is the midpoint of its openable run
    ///     (<see cref="ISpatialPort.OffsetCells" /> through <see cref="ISpatialPort.SpanCells" />) on the footprint
    ///     face. Ports whose face is not cardinal are ignored; fewer than two usable ports give 0.
    /// </summary>
    /// <param name="footprint">The template's footprint in cells.</param>
    /// <param name="ports">The template's ports.</param>
    public static int ThroughDistanceCells(Vector3I footprint, IReadOnlyList<ISpatialPort> ports)
    {
        var centres = new List<Vector2I>(ports.Count);
        foreach (ISpatialPort port in ports)
        {
            int along = (2 * port.OffsetCells) + port.SpanCells;
            switch (port.Face)
            {
                case PortFace.XPos:
                    centres.Add(new Vector2I(2 * footprint.X, along));
                    break;
                case PortFace.XNeg:
                    centres.Add(new Vector2I(0, along));
                    break;
                case PortFace.ZPos:
                    centres.Add(new Vector2I(along, 2 * footprint.Z));
                    break;
                case PortFace.ZNeg:
                    centres.Add(new Vector2I(along, 0));
                    break;
            }
        }

        int pairs = 0;
        int doubledSum = 0;
        for (int i = 0; i < centres.Count; i++)
        {
            for (int j = i + 1; j < centres.Count; j++)
            {
                doubledSum += Mathf.Abs(centres[i].X - centres[j].X) + Mathf.Abs(centres[i].Y - centres[j].Y);
                pairs++;
            }
        }

        return pairs == 0 ? 0 : (doubledSum + pairs) / (2 * pairs);
    }
}
