namespace Jmodot.Implementation.ProcGen.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen.Spatial;

/// <summary>Deterministic axis-aligned connector-path search used when a loop edge cannot abut directly.</summary>
internal static class ConnectorSolver
{
    private readonly record struct Path(IReadOnlyList<(Vector3I Origin, Vector3I Size)> Boxes, int Length, int FirstCorner);

    /// <summary>Finds the first collision-free corridor path between two world ports, or null when none fits.</summary>
    internal static IReadOnlyList<(Vector3I Origin, Vector3I Size)>? Solve(
        WorldPort from,
        WorldPort to,
        OccupancyIndex committed,
        IReadOnlyList<(Vector3I Origin, Vector3I Size)> candidateLocalObstacles,
        Vector3I envelopeSize,
        int maxLengthCells)
    {
        ArgumentNullException.ThrowIfNull(committed);
        ArgumentNullException.ThrowIfNull(candidateLocalObstacles);
        if (from.WidthCells <= 0 || from.WidthCells != to.WidthCells || from.AnchorCells.Y != to.AnchorCells.Y ||
            envelopeSize.X <= 0 || envelopeSize.Y <= 0 || envelopeSize.Z <= 0 || maxLengthCells <= 0)
        {
            return null;
        }

        int width = from.WidthCells;
        foreach (Path path in EnumeratePaths(from, to, width, maxLengthCells))
        {
            if (path.Length > maxLengthCells || !FitsEnvelope(path.Boxes, candidateLocalObstacles, envelopeSize))
            {
                continue;
            }

            if (IsClear(path.Boxes, committed, candidateLocalObstacles))
            {
                return path.Boxes;
            }
        }

        return null;
    }

    private static IEnumerable<Path> EnumeratePaths(WorldPort from, WorldPort to, int width, int maxLength)
    {
        bool sameAxis = Axis(from.Face) == Axis(to.Face);
        bool opposite = Sign(from.Face) == -Sign(to.Face);
        bool facing = FacesTowardEachOther(from, to);

        if (sameAxis && opposite && facing && SameTangent(from, to, width))
        {
            List<(Vector3I Origin, Vector3I Size)> straight = Straight(from, to, width);
            yield return new Path(straight, SegmentLength(straight, width), 0);
        }

        if (!facing && sameAxis && opposite)
        {
            yield break;
        }

        if (!sameAxis && facing)
        {
            Path? l = LPath(from, to, width);
            if (l.HasValue)
            {
                yield return l.Value;
            }

            yield break;
        }

        if (sameAxis && opposite && facing)
        {
            foreach (Path path in ZPaths(from, to, width, maxLength))
            {
                yield return path;
            }

            yield break;
        }

        if (sameAxis && !opposite)
        {
            foreach (Path path in UPaths(from, to, width, maxLength))
            {
                yield return path;
            }
        }
    }

    private static Path? LPath(WorldPort from, WorldPort to, int width)
    {
        int fromAxis = Axis(from.Face);
        int toAxis = Axis(to.Face);
        Vector3I corner = fromAxis == 0
            ? new Vector3I(to.AnchorCells.X, from.AnchorCells.Y, from.AnchorCells.Z)
            : new Vector3I(from.AnchorCells.X, from.AnchorCells.Y, to.AnchorCells.Z);
        var boxes = new List<(Vector3I Origin, Vector3I Size)>();
        AddSegment(boxes, from.AnchorCells, corner, fromAxis, width);
        AddSegment(boxes, corner, to.AnchorCells, toAxis, width);
        if (boxes.Count == 0)
        {
            return null;
        }

        return new Path(boxes, SegmentLength(boxes, width), FirstCornerDistance(from.AnchorCells, corner, fromAxis));
    }

    private static IEnumerable<Path> ZPaths(WorldPort from, WorldPort to, int width, int maxLength)
    {
        int axis = Axis(from.Face);
        int tangent = TangentAxis(from.Face);
        int start = AxisValue(from.AnchorCells, axis);
        int end = AxisValue(to.AnchorCells, axis);
        int direction = Sign(from.Face);
        int distance = Math.Abs(end - start);
        if (distance < 2)
        {
            yield break;
        }

        for (int offset = 1; offset < distance; offset++)
        {
            int turn = start + direction * offset;
            Vector3I firstCorner = SetAxis(from.AnchorCells, axis, turn);
            var boxes = new List<(Vector3I Origin, Vector3I Size)>();
            AddSegment(boxes, from.AnchorCells, firstCorner, axis, width);
            AddSegment(boxes, firstCorner, SetAxis(to.AnchorCells, axis, turn), tangent, width);
            AddSegment(boxes, SetAxis(to.AnchorCells, axis, turn), to.AnchorCells, axis, width);
            if (boxes.Count == 0)
            {
                continue;
            }

            yield return new Path(boxes, SegmentLength(boxes, width), offset);
        }

        if (!SameTangent(from, to, width))
        {
            yield break;
        }

        int detourTangent = TangentValue(from.AnchorCells, tangent) + 1;
        int turnPoint = start + direction;
        int endpointTurn = AxisValue(to.AnchorCells, axis) - direction * width;
        Vector3I first = SetAxis(from.AnchorCells, axis, turnPoint);
        Vector3I firstDetour = SetAxis(SetTangent(from.AnchorCells, tangent, detourTangent), axis, turnPoint);
        Vector3I secondDetour = SetAxis(SetTangent(to.AnchorCells, tangent, detourTangent), axis, endpointTurn);
        Vector3I secondDrop = SetTangent(secondDetour, tangent, TangentValue(to.AnchorCells, tangent));
        var detour = new List<(Vector3I Origin, Vector3I Size)>();
        AddSegment(detour, from.AnchorCells, first, axis, width);
        AddSegment(detour, first, firstDetour, tangent, width);
        AddSegment(detour, firstDetour, secondDetour, axis, width);
        AddSegment(detour, secondDetour, secondDrop, tangent, width);
        AddSegment(detour, secondDrop, to.AnchorCells, axis, width);
        if (detour.Count > 0)
        {
            yield return new Path(detour, SegmentLength(detour, width), 1);
        }
    }

    private static IEnumerable<Path> UPaths(WorldPort from, WorldPort to, int width, int maxLength)
    {
        int axis = Axis(from.Face);
        int tangent = TangentAxis(from.Face);
        int direction = Sign(from.Face);
        int aAxis = AxisValue(from.AnchorCells, axis);
        int bAxis = AxisValue(to.AnchorCells, axis);
        int outside = Math.Max(aAxis, bAxis) + 1;
        if (direction < 0)
        {
            outside = Math.Min(aAxis, bAxis) - 1;
        }

        int aTangent = TangentValue(from.AnchorCells, tangent);
        int bTangent = TangentValue(to.AnchorCells, tangent);
        int tangentDistance = Math.Abs(bTangent - aTangent);
        if (tangentDistance == 0)
        {
            yield break;
        }

        Vector3I firstCorner = SetAxis(from.AnchorCells, axis, outside);
        Vector3I secondCorner = SetAxis(to.AnchorCells, axis, outside);
        var boxes = new List<(Vector3I Origin, Vector3I Size)>();
        AddSegment(boxes, from.AnchorCells, firstCorner, axis, width);
        AddSegment(boxes, firstCorner, secondCorner, tangent, width);
        AddSegment(boxes, secondCorner, to.AnchorCells, axis, width);
        if (boxes.Count > 0)
        {
            yield return new Path(boxes, SegmentLength(boxes, width), Math.Abs(outside - aAxis));
        }
    }

    private static List<(Vector3I Origin, Vector3I Size)> Straight(WorldPort from, WorldPort to, int width)
    {
        var boxes = new List<(Vector3I Origin, Vector3I Size)>();
        AddSegment(boxes, from.AnchorCells, to.AnchorCells, Axis(from.Face), width);
        return boxes;
    }

    private static void AddSegment(
        List<(Vector3I Origin, Vector3I Size)> boxes,
        Vector3I start,
        Vector3I end,
        int axis,
        int width)
    {
        int length = axis == 0 ? Math.Abs(end.X - start.X) : Math.Abs(end.Z - start.Z);
        if (length <= 0)
        {
            return;
        }

        Vector3I origin;
        Vector3I size;
        if (axis == 0)
        {
            origin = new Vector3I(Math.Min(start.X, end.X), start.Y, start.Z);
            size = new Vector3I(length, 1, width);
        }
        else
        {
            origin = new Vector3I(start.X, start.Y, Math.Min(start.Z, end.Z));
            size = new Vector3I(width, 1, length);
        }

        boxes.Add((origin, size));
    }

    private static bool IsClear(
        IReadOnlyList<(Vector3I Origin, Vector3I Size)> boxes,
        OccupancyIndex committed,
        IReadOnlyList<(Vector3I Origin, Vector3I Size)> local)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            (Vector3I origin, Vector3I size) = boxes[i];
            if (committed.Overlaps(origin, size))
            {
                return false;
            }

            foreach ((Vector3I localOrigin, Vector3I localSize) in local)
            {
                if (Intersects(origin, size, localOrigin, localSize))
                {
                    return false;
                }
            }

            // A path must not fold back onto itself: U legs whose tangent gap is under the
            // corridor width intersect while clearing every external obstacle. Adjacent segments
            // are exempt — they legitimately double-cover the shared corner block.
            for (int j = 0; j < i - 1; j++)
            {
                if (Intersects(origin, size, boxes[j].Origin, boxes[j].Size))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool FitsEnvelope(
        IReadOnlyList<(Vector3I Origin, Vector3I Size)> boxes,
        IReadOnlyList<(Vector3I Origin, Vector3I Size)> local,
        Vector3I envelope)
    {
        var all = boxes.Concat(local).ToList();
        if (all.Count == 0)
        {
            return false;
        }

        Vector3I min = all[0].Origin;
        Vector3I max = all[0].Origin + all[0].Size;
        foreach ((Vector3I origin, Vector3I size) in all.Skip(1))
        {
            min = new Vector3I(Math.Min(min.X, origin.X), Math.Min(min.Y, origin.Y), Math.Min(min.Z, origin.Z));
            Vector3I end = origin + size;
            max = new Vector3I(Math.Max(max.X, end.X), Math.Max(max.Y, end.Y), Math.Max(max.Z, end.Z));
        }

        Vector3I span = max - min;
        return span.X <= envelope.X && span.Y <= envelope.Y && span.Z <= envelope.Z;
    }

    private static bool Intersects(Vector3I aOrigin, Vector3I aSize, Vector3I bOrigin, Vector3I bSize)
        => aOrigin.X < bOrigin.X + bSize.X && bOrigin.X < aOrigin.X + aSize.X
            && aOrigin.Y < bOrigin.Y + bSize.Y && bOrigin.Y < aOrigin.Y + aSize.Y
            && aOrigin.Z < bOrigin.Z + bSize.Z && bOrigin.Z < aOrigin.Z + aSize.Z;

    private static int SegmentLength(IReadOnlyList<(Vector3I Origin, Vector3I Size)> boxes, int width)
    {
        int length = 0;
        foreach ((Vector3I _, Vector3I size) in boxes)
        {
            length += size.X == width && size.Z != width ? size.Z : size.X;
        }

        return length;
    }

    private static int FirstCornerDistance(Vector3I from, Vector3I corner, int axis)
        => axis == 0 ? Math.Abs(corner.X - from.X) : Math.Abs(corner.Z - from.Z);

    private static bool FacesTowardEachOther(WorldPort from, WorldPort to)
    {
        int axisFrom = Axis(from.Face);
        int axisTo = Axis(to.Face);
        if (axisFrom == axisTo)
        {
            int delta = AxisValue(to.AnchorCells, axisFrom) - AxisValue(from.AnchorCells, axisFrom);
            return delta != 0 && Math.Sign(delta) == Sign(from.Face) && Math.Sign(-delta) == Sign(to.Face);
        }

        int deltaFrom = AxisValue(to.AnchorCells, axisFrom) - AxisValue(from.AnchorCells, axisFrom);
        int deltaTo = AxisValue(from.AnchorCells, axisTo) - AxisValue(to.AnchorCells, axisTo);
        return Math.Sign(deltaFrom) == Sign(from.Face) && Math.Sign(deltaTo) == Sign(to.Face);
    }

    private static bool SameTangent(WorldPort from, WorldPort to, int width)
    {
        int axis = Axis(from.Face);
        int tangent = axis == 0 ? 2 : 0;
        return TangentValue(from.AnchorCells, tangent) == TangentValue(to.AnchorCells, tangent) &&
            from.WidthCells == width && to.WidthCells == width;
    }

    private static int Axis(PortFace face) => face is PortFace.XPos or PortFace.XNeg ? 0 : 1;

    private static int TangentAxis(PortFace face) => Axis(face) == 0 ? 1 : 0;

    private static int Sign(PortFace face) => face is PortFace.XPos or PortFace.ZPos ? 1 : -1;

    private static int AxisValue(Vector3I value, int axis) => axis == 0 ? value.X : value.Z;

    private static int TangentValue(Vector3I value, int axis) => axis == 0 ? value.X : value.Z;

    private static Vector3I SetAxis(Vector3I value, int axis, int coordinate)
        => axis == 0 ? new Vector3I(coordinate, value.Y, value.Z) : new Vector3I(value.X, value.Y, coordinate);

    private static Vector3I SetTangent(Vector3I value, int axis, int coordinate)
        => axis == 0 ? new Vector3I(coordinate, value.Y, value.Z) : new Vector3I(value.X, value.Y, coordinate);
}
