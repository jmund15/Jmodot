namespace Jmodot.Implementation.ProcGen.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     World-space port descriptor for a posed node: world face, world boundary-lattice anchor
///     (min corner of the doorway span), and span width.
/// </summary>
internal readonly record struct WorldPort(PortFace Face, Vector3I AnchorCells, int WidthCells);

/// <summary>
///     World-space openable run of a posed port: world face, world boundary-lattice anchor (min corner of the
///     run), the run length, and the door width range <c>[MinWidthCells, WidthCells]</c> a door may take inside it.
/// </summary>
internal readonly record struct WorldSpan(PortFace Face, Vector3I AnchorCells, int SpanCells, int MinWidthCells, int WidthCells);

/// <summary>
///     Integer pose arithmetic for the grid embedder (design-se §4). Yaw convention (Decision D2):
///     <see cref="YawQuadrant.Yaw90" /> is one CCW quarter-turn viewed from +Y (Godot-consistent,
///     +X maps to −Z), giving the world face cycle XPos→ZNeg→XNeg→ZPos. Port anchors live on the
///     template-local boundary lattice per the <see cref="SpatialParity" /> convention; rotation
///     transforms the span's two endpoints and re-normalizes to the min corner, which is what
///     makes tangent-axis reversal come out right without case analysis.
/// </summary>
internal static class SpatialPoseMath
{
    internal static PortFace RotateFace(PortFace face, YawQuadrant yaw)
    {
        var rotated = face;
        for (int i = 0; i < (int)yaw; i++)
        {
            rotated = RotateFaceOnce(rotated);
        }

        return rotated;
    }

    internal static PortFace Opposite(PortFace face)
    {
        return face switch
        {
            PortFace.XPos => PortFace.XNeg,
            PortFace.XNeg => PortFace.XPos,
            PortFace.ZPos => PortFace.ZNeg,
            PortFace.ZNeg => PortFace.ZPos,
            _ => throw new ArgumentException($"Opposite requires a concrete PortFace; got {face}.", nameof(face)),
        };
    }

    internal static Vector3I RotateSize(Vector3I sizeCells, YawQuadrant yaw)
    {
        bool swaps = yaw is YawQuadrant.Yaw90 or YawQuadrant.Yaw270;
        return swaps ? new Vector3I(sizeCells.Z, sizeCells.Y, sizeCells.X) : sizeCells;
    }

    /// <summary>
    ///     The port's template-local boundary anchor after <paramref name="yaw" />, min-corner
    ///     normalized: (x, elevation, z) relative to the posed node's origin.
    /// </summary>
    internal static Vector3I RotateLocalAnchor(ISpatialPort port, Vector3I footprintCells, YawQuadrant yaw)
    {
        return RotateAnchor(port.Face, port.OffsetCells, port.ElevationCells, port.WidthCells, footprintCells, yaw);
    }

    private static Vector3I RotateAnchor(
        PortFace face, int offsetCells, int elevationCells, int lengthCells, Vector3I footprintCells, YawQuadrant yaw)
    {
        Vector2I anchor = LocalAnchor(face, offsetCells, footprintCells);
        Vector2I tangent = TangentAxis(face);
        Vector2I spanEnd = anchor + (tangent * lengthCells);

        Vector2I extents = new(footprintCells.X, footprintCells.Z);
        for (int i = 0; i < (int)yaw; i++)
        {
            anchor = RotatePointOnce(anchor, extents);
            spanEnd = RotatePointOnce(spanEnd, extents);
            extents = new Vector2I(extents.Y, extents.X);
        }

        Vector2I minCorner = new(Math.Min(anchor.X, spanEnd.X), Math.Min(anchor.Y, spanEnd.Y));
        return new Vector3I(minCorner.X, elevationCells, minCorner.Y);
    }

    internal static WorldPort WorldPortOf(CellPose pose, Vector3I footprintCells, ISpatialPort port)
    {
        var worldFace = RotateFace(port.Face, pose.Yaw);
        Vector3I anchor = pose.Origin + RotateLocalAnchor(port, footprintCells, pose.Yaw);
        return new WorldPort(worldFace, anchor, port.WidthCells);
    }

    /// <summary>
    ///     The neighbor origin that makes <paramref name="toPort" /> (under <paramref name="toYaw" />)
    ///     abut <paramref name="fromWorld" /> with exactly coinciding spans — or null when the world
    ///     faces don't oppose / widths differ. Exact span coincidence means a binding leaves no slide
    ///     freedom: the neighbor's pose is fully determined.
    /// </summary>
    internal static Vector3I? SolveAbutment(WorldPort fromWorld, ISpatialPort toPort, Vector3I toFootprintCells, YawQuadrant toYaw)
    {
        if (RotateFace(toPort.Face, toYaw) != Opposite(fromWorld.Face))
        {
            return null;
        }

        if (toPort.WidthCells != fromWorld.WidthCells)
        {
            return null;
        }

        return fromWorld.AnchorCells - RotateLocalAnchor(toPort, toFootprintCells, toYaw);
    }

    /// <summary>The openable run of <paramref name="port" /> on a node posed at <paramref name="pose" />.</summary>
    internal static WorldSpan SpanOf(CellPose pose, Vector3I footprintCells, ISpatialPort port)
    {
        PortFace worldFace = RotateFace(port.Face, pose.Yaw);
        Vector3I anchor = pose.Origin + RotateAnchor(
            port.Face, port.OffsetCells, port.ElevationCells, port.SpanCells, footprintCells, pose.Yaw);
        return new WorldSpan(worldFace, anchor, port.SpanCells, port.MinWidthCells, port.WidthCells);
    }

    /// <summary>
    ///     Every origin at which <paramref name="toPort" /> (under <paramref name="toYaw" />) abuts
    ///     <paramref name="fromSpan" /> with the two spans sharing at least the minimum common door width, centre-out:
    ///     the first origin aligns the span centres, then origins alternate outward, the lower tangent origin first at
    ///     equal distance. Empty when the world faces do not oppose or no door width is common to both ports. A span
    ///     as long as its door leaves one origin.
    /// </summary>
    internal static IEnumerable<Vector3I> AbutmentSlides(WorldSpan fromSpan, ISpatialPort toPort, Vector3I toFootprintCells, YawQuadrant toYaw)
    {
        if (!IsCardinal(fromSpan.Face) || RotateFace(toPort.Face, toYaw) != Opposite(fromSpan.Face))
        {
            yield break;
        }

        int minCommon = Math.Max(fromSpan.MinWidthCells, toPort.MinWidthCells);
        int maxCommon = Math.Min(fromSpan.WidthCells, toPort.WidthCells);
        if (minCommon < 1 || minCommon > maxCommon)
        {
            yield break;
        }

        int toLength = toPort.SpanCells;
        Vector3I toAnchor = RotateAnchor(toPort.Face, toPort.OffsetCells, toPort.ElevationCells, toLength, toFootprintCells, toYaw);
        Vector3I tangentStep = TangentIsZ(fromSpan.Face) ? new Vector3I(0, 0, 1) : new Vector3I(1, 0, 0);
        int lowShift = minCommon - toLength;
        int highShift = fromSpan.SpanCells - minCommon;
        int centreTwice = fromSpan.SpanCells - toLength;
        foreach (int shift in Enumerable.Range(lowShift, Math.Max(0, highShift - lowShift + 1))
                     .OrderBy(d => Math.Abs((2 * d) - centreTwice))
                     .ThenBy(d => d))
        {
            yield return fromSpan.AnchorCells - toAnchor + (tangentStep * shift);
        }
    }

    /// <summary>The first origin <see cref="AbutmentSlides" /> yields, or null when it yields none.</summary>
    internal static Vector3I? FirstSlide(WorldSpan fromSpan, ISpatialPort toPort, Vector3I toFootprintCells, YawQuadrant toYaw)
    {
        foreach (Vector3I origin in AbutmentSlides(fromSpan, toPort, toFootprintCells, toYaw))
        {
            return origin;
        }

        return null;
    }

    /// <summary>
    ///     The door window two coincident, opposed spans share: the widest width both ports allow that fits their
    ///     overlap, centred in it with a half-cell remainder going to the min corner. The window sits on
    ///     <paramref name="a" />'s face. Null when the faces do not oppose on one plane, no door width is common
    ///     to both, or the overlap is under the minimum common width.
    /// </summary>
    internal static WorldPort? SharedOpening(WorldSpan a, WorldSpan b)
    {
        if (!IsCardinal(a.Face) || !IsCardinal(b.Face) || b.Face != Opposite(a.Face))
        {
            return null;
        }

        if (NormalCoordinate(a.Face, a.AnchorCells) != NormalCoordinate(b.Face, b.AnchorCells) ||
            a.AnchorCells.Y != b.AnchorCells.Y)
        {
            return null;
        }

        int minCommon = Math.Max(a.MinWidthCells, b.MinWidthCells);
        int maxCommon = Math.Min(a.WidthCells, b.WidthCells);
        if (minCommon < 1 || minCommon > maxCommon)
        {
            return null;
        }

        int aStart = TangentCoordinate(a.Face, a.AnchorCells);
        int bStart = TangentCoordinate(b.Face, b.AnchorCells);
        int overlapStart = Math.Max(aStart, bStart);
        int overlap = Math.Min(aStart + a.SpanCells, bStart + b.SpanCells) - overlapStart;
        int width = Math.Min(maxCommon, overlap);
        if (width < minCommon)
        {
            return null;
        }

        int start = overlapStart + ((overlap - width) / 2);
        return new WorldPort(a.Face, WithTangent(a.Face, a.AnchorCells, start), width);
    }

    /// <summary>
    ///     The one door-window pair a connector corridor joins between two spans. Both windows take the widest door
    ///     width inside the intersection of the spans' <c>[MinWidthCells, WidthCells]</c> ranges, so they always share
    ///     one width. Each window sits on its own span where its centre is nearest the other span's centre (projected
    ///     onto the window's own tangent axis), clamped inside the span; a half-cell misalignment rounds toward the min
    ///     corner. Null when a face is not cardinal or the width ranges are disjoint.
    /// </summary>
    internal static (WorldPort AWindow, WorldPort BWindow)? ConnectorWindows(WorldSpan a, WorldSpan b)
    {
        if (!IsCardinal(a.Face) || !IsCardinal(b.Face))
        {
            return null;
        }

        int minCommon = Math.Max(a.MinWidthCells, b.MinWidthCells);
        int maxCommon = Math.Min(a.WidthCells, b.WidthCells);
        if (minCommon < 1 || minCommon > maxCommon)
        {
            return null;
        }

        return (WindowNearest(a, b, maxCommon), WindowNearest(b, a, maxCommon));
    }

    private static WorldPort WindowNearest(WorldSpan own, WorldSpan other, int width)
    {
        int ownStart = TangentCoordinate(own.Face, own.AnchorCells);
        int wantedStart = (CentreTwice(other, TangentIsZ(own.Face)) - width) >> 1;
        int start = Math.Clamp(wantedStart, ownStart, ownStart + Math.Max(0, own.SpanCells - width));
        return new WorldPort(own.Face, WithTangent(own.Face, own.AnchorCells, start), width);
    }

    // Twice the span centre along the z axis (alongZ) or the x axis; a span whose tangent runs along the other
    // axis lies on a plane there, so its centre is its plane coordinate.
    private static int CentreTwice(WorldSpan span, bool alongZ)
    {
        return TangentIsZ(span.Face) == alongZ
            ? (2 * TangentCoordinate(span.Face, span.AnchorCells)) + span.SpanCells
            : 2 * NormalCoordinate(span.Face, span.AnchorCells);
    }

    /// <summary>
    ///     Converts a world door window on <paramref name="port" /> of a node posed at <paramref name="pose" /> into the
    ///     port's template-local opening. The inverse of <see cref="WindowOf" />.
    /// </summary>
    internal static PortOpening ToOpening(WorldPort window, CellPose pose, Vector3I footprintCells, ISpatialPort port)
    {
        WorldSpan span = SpanOf(pose, footprintCells, port);
        int spanStart = TangentCoordinate(span.Face, span.AnchorCells);
        int windowStart = TangentCoordinate(span.Face, window.AnchorCells);
        int offset = TangentReversed(port.Face, pose.Yaw)
            ? port.OffsetCells + (spanStart + span.SpanCells - (windowStart + window.WidthCells))
            : port.OffsetCells + (windowStart - spanStart);
        return new PortOpening(offset, window.WidthCells);
    }

    /// <summary>The world door window of <paramref name="opening" /> on <paramref name="port" /> of a node posed at <paramref name="pose" />.</summary>
    internal static WorldPort WindowOf(CellPose pose, Vector3I footprintCells, ISpatialPort port, PortOpening opening)
    {
        PortFace worldFace = RotateFace(port.Face, pose.Yaw);
        Vector3I anchor = pose.Origin + RotateAnchor(
            port.Face, opening.OffsetCells, port.ElevationCells, opening.WidthCells, footprintCells, pose.Yaw);
        return new WorldPort(worldFace, anchor, opening.WidthCells);
    }

    internal static DoorwayPose DeriveDoorway(
        StringName fromNodeId,
        StringName toNodeId,
        StringName fromPortName,
        StringName toPortName,
        WorldPort fromWorld)
    {
        return new DoorwayPose(
            fromNodeId, toNodeId, fromPortName, toPortName,
            fromWorld.AnchorCells, fromWorld.Face, fromWorld.WidthCells);
    }

    private static PortFace RotateFaceOnce(PortFace face)
    {
        return face switch
        {
            PortFace.XPos => PortFace.ZNeg,
            PortFace.ZNeg => PortFace.XNeg,
            PortFace.XNeg => PortFace.ZPos,
            PortFace.ZPos => PortFace.XPos,
            _ => throw new ArgumentException($"RotateFace requires a concrete PortFace; got {face}.", nameof(face)),
        };
    }

    private static Vector2I RotatePointOnce(Vector2I point, Vector2I extents)
    {
        return new Vector2I(point.Y, extents.X - point.X);
    }

    private static Vector2I LocalAnchor(PortFace face, int offsetCells, Vector3I footprintCells)
    {
        return face switch
        {
            PortFace.XPos => new Vector2I(footprintCells.X, offsetCells),
            PortFace.XNeg => new Vector2I(0, offsetCells),
            PortFace.ZPos => new Vector2I(offsetCells, footprintCells.Z),
            PortFace.ZNeg => new Vector2I(offsetCells, 0),
            _ => throw new ArgumentException($"LocalAnchor requires a concrete PortFace; got {face}.", nameof(face)),
        };
    }

    private static bool IsCardinal(PortFace face)
    {
        return face is PortFace.XPos or PortFace.XNeg or PortFace.ZPos or PortFace.ZNeg;
    }

    private static bool TangentIsZ(PortFace face)
    {
        return face is PortFace.XPos or PortFace.XNeg;
    }

    private static int TangentCoordinate(PortFace face, Vector3I anchor)
    {
        return TangentIsZ(face) ? anchor.Z : anchor.X;
    }

    private static int NormalCoordinate(PortFace face, Vector3I anchor)
    {
        return TangentIsZ(face) ? anchor.X : anchor.Z;
    }

    private static Vector3I WithTangent(PortFace face, Vector3I anchor, int tangentCoordinate)
    {
        return TangentIsZ(face)
            ? new Vector3I(anchor.X, anchor.Y, tangentCoordinate)
            : new Vector3I(tangentCoordinate, anchor.Y, anchor.Z);
    }

    // Whether yaw flips the direction of the port's tangent, so a local offset measured from the face's min
    // corner runs from the world max corner instead.
    private static bool TangentReversed(PortFace localFace, YawQuadrant yaw)
    {
        (int dx, int dz) = TangentIsZ(localFace) ? (0, 1) : (1, 0);
        for (int i = 0; i < (int)yaw; i++)
        {
            (dx, dz) = (dz, -dx);
        }

        return dx + dz < 0;
    }

    private static Vector2I TangentAxis(PortFace face)
    {
        return face is PortFace.XPos or PortFace.XNeg ? new Vector2I(0, 1) : new Vector2I(1, 0);
    }
}
