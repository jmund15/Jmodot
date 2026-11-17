namespace Jmodot.Core.ProcGen.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Template parity-class arithmetic for the cell embedder. A port's "anchor parity" is the
///     <c>(x, z) mod 2</c> of its min-corner anchor in template-local cells; a template's parity
///     class is the ordinal-sorted multiset of its ports' anchor parities, canonicalized over the
///     Yaw90 component-swap orbit. Two templates share a class iff their descriptors are string-equal
///     — translation cancels (coordinates are template-local), Yaw180 is identity mod 2, and Yaw90/270
///     is the component swap the descriptor minimizes over. The cycle-residue arithmetic that consumes
///     these classes is P3b.4's concern.
/// </summary>
public static class SpatialParity
{
    /// <summary>
    ///     The <c>(x, z) mod 2</c> of <paramref name="port" />'s min-corner anchor in the template-local
    ///     cells of a footprint of size <paramref name="footprintCells" />:
    ///     XPos -> (W, o); XNeg -> (0, o); ZPos -> (o, D); ZNeg -> (o, 0)
    ///     (W = X extent, D = Z extent, o = <see cref="ISpatialPort.OffsetCells" />). Throws on an
    ///     <see cref="PortFace.Unset" /> face.
    /// </summary>
    public static Vector2I PortAnchorParity(ISpatialPort port, Vector3I footprintCells)
    {
        int w = footprintCells.X;
        int d = footprintCells.Z;
        int o = port.OffsetCells;
        Vector2I anchor = port.Face switch
        {
            PortFace.XPos => new Vector2I(w, o),
            PortFace.XNeg => new Vector2I(0, o),
            PortFace.ZPos => new Vector2I(o, d),
            PortFace.ZNeg => new Vector2I(o, 0),
            _ => throw new ArgumentException(
                $"PortAnchorParity requires a concrete PortFace; got {port.Face}.", nameof(port)),
        };
        return new Vector2I(Mod2(anchor.X), Mod2(anchor.Y));
    }

    /// <summary>
    ///     The canonical parity-class descriptor of a template: the ordinal-sorted multiset of its
    ///     ports' anchor parities, taken as the ordinal-min over the identity and Yaw90 component-swap
    ///     orientations. Two templates are in the same parity class iff their descriptors are equal. A port whose run is
    ///     longer than its narrowest door contributes a free component along its face, since its window may start on
    ///     any cell of the run.
    /// </summary>
    public static string DescriptorOf(Vector3I footprintCells, IReadOnlyList<ISpatialPort> ports)
    {
        var parities = ports.Select(p => ParityRecord(p, footprintCells)).ToList();
        string identity = ComposeMultiset(parities.Select(v => (v.x, v.z)));
        string swapped = ComposeMultiset(parities.Select(v => (v.z, v.x)));
        return string.CompareOrdinal(identity, swapped) <= 0 ? identity : swapped;
    }

    // A port whose run is longer than its narrowest door lets the window start on any cell of the run, so its anchor
    // parity along the face is free (FreeParity); the component along the face normal is the footprint extent's.
    private const string FreeParity = "*";

    private static (string x, string z) ParityRecord(ISpatialPort port, Vector3I footprintCells)
    {
        Vector2I parity = PortAnchorParity(port, footprintCells);
        bool slack = port.SpanCells > port.MinWidthCells;
        bool tangentIsZ = port.Face is PortFace.XPos or PortFace.XNeg;
        string x = slack && !tangentIsZ ? FreeParity : parity.X.ToString();
        string z = slack && tangentIsZ ? FreeParity : parity.Y.ToString();
        return (x, z);
    }

    private static string ComposeMultiset(IEnumerable<(string x, string z)> parities)
    {
        var records = parities
            .Select(p => $"{p.x}{GraphSignature.FieldSep}{p.z}")
            .OrderBy(s => s, StringComparer.Ordinal);
        return string.Join(GraphSignature.RecordSep, records);
    }

    private static int Mod2(int v) => ((v % 2) + 2) % 2;
}
