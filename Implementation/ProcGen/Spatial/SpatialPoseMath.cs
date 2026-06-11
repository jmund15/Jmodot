namespace Jmodot.Implementation.ProcGen.Spatial;

using System;
using Godot;
using Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     World-space port descriptor for a posed node: world face, world boundary-lattice anchor
///     (min corner of the doorway span), and span width.
/// </summary>
internal readonly record struct WorldPort(PortFace Face, Vector3I AnchorCells, int WidthCells);

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
        Vector2I anchor = LocalAnchor(port, footprintCells);
        Vector2I tangent = TangentAxis(port.Face);
        Vector2I spanEnd = anchor + (tangent * port.WidthCells);

        Vector2I extents = new(footprintCells.X, footprintCells.Z);
        for (int i = 0; i < (int)yaw; i++)
        {
            anchor = RotatePointOnce(anchor, extents);
            spanEnd = RotatePointOnce(spanEnd, extents);
            extents = new Vector2I(extents.Y, extents.X);
        }

        Vector2I minCorner = new(Math.Min(anchor.X, spanEnd.X), Math.Min(anchor.Y, spanEnd.Y));
        return new Vector3I(minCorner.X, port.ElevationCells, minCorner.Y);
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

    private static Vector2I LocalAnchor(ISpatialPort port, Vector3I footprintCells)
    {
        return port.Face switch
        {
            PortFace.XPos => new Vector2I(footprintCells.X, port.OffsetCells),
            PortFace.XNeg => new Vector2I(0, port.OffsetCells),
            PortFace.ZPos => new Vector2I(port.OffsetCells, footprintCells.Z),
            PortFace.ZNeg => new Vector2I(port.OffsetCells, 0),
            _ => throw new ArgumentException($"LocalAnchor requires a concrete PortFace; got {port.Face}.", nameof(port)),
        };
    }

    private static Vector2I TangentAxis(PortFace face)
    {
        return face is PortFace.XPos or PortFace.XNeg ? new Vector2I(0, 1) : new Vector2I(1, 0);
    }
}
