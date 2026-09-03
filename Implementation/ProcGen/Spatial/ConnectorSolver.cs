namespace Jmodot.Implementation.ProcGen.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.ProcGen.Spatial;

/// <summary>Deterministic axis-aligned connector-path search used when a loop edge cannot abut directly.</summary>
internal static class ConnectorSolver
{
    private readonly record struct Path(IReadOnlyList<(Vector3I Origin, Vector3I Size)> Boxes, int Length, int FirstCorner);

    /// <summary>
    ///     Finds the first collision-free corridor path between two world ports, or null when none fits.
    ///     <paramref name="policy" /> gates exactly one path family: the BACK-TO-BACK wrap-around, offered
    ///     only under <see cref="ConnectorPolicy.ClosableWraparound" />. Every other family — including the
    ///     wrap that joins two ports facing the SAME direction — is offered under
    ///     <see cref="ConnectorPolicy.Closable" /> as well. Callers pass
    ///     <see cref="ConnectorPolicy.AbutmentOnly" /> nowhere — that policy is expected to short-circuit
    ///     before reaching the solver at all.
    /// </summary>
    internal static IReadOnlyList<(Vector3I Origin, Vector3I Size)>? Solve(
        WorldPort from,
        WorldPort to,
        OccupancyIndex committed,
        IReadOnlyList<(Vector3I Origin, Vector3I Size)> candidateLocalObstacles,
        Vector3I envelopeSize,
        int maxLengthCells,
        ConnectorPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(committed);
        ArgumentNullException.ThrowIfNull(candidateLocalObstacles);
        if (!IsCardinal(from.Face) || !IsCardinal(to.Face))
        {
            return null;
        }

        if (from.WidthCells <= 0 || from.WidthCells != to.WidthCells || from.AnchorCells.Y != to.AnchorCells.Y ||
            envelopeSize.X <= 0 || envelopeSize.Y <= 0 || envelopeSize.Z <= 0 || maxLengthCells <= 0)
        {
            return null;
        }

        int width = from.WidthCells;
        foreach (Path path in EnumeratePaths(from, to, width, maxLengthCells, policy))
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

    private static IEnumerable<Path> EnumeratePaths(
        WorldPort from,
        WorldPort to,
        int width,
        int maxLength,
        ConnectorPolicy policy)
    {
        bool sameAxis = Axis(from.Face) == Axis(to.Face);
        bool opposite = Sign(from.Face) == -Sign(to.Face);
        bool facing = FacesTowardEachOther(from, to);

        if (sameAxis && opposite && facing)
        {
            if (SameTangent(from, to, width))
            {
                Vector3I[] straightPoints = { from.AnchorCells, to.AnchorCells };
                yield return new Path(
                    BuildBoxes(straightPoints, width), PolylineLength(straightPoints), 0);
            }

            foreach (Path path in ZPaths(from, to, width, maxLength))
            {
                yield return path;
            }

            yield break;
        }

        if (sameAxis && opposite)
        {
            // Back-to-back ports can only join by wrapping a corridor all the way around behind one
            // room. That ring commits a large contiguous span the later placements cannot reuse, so
            // it is opt-in: on a tight envelope it closes this loop and starves the rest.
            if (policy != ConnectorPolicy.ClosableWraparound)
            {
                yield break;
            }

            foreach (Path path in WrapPaths(from, to, width, maxLength))
            {
                yield return path;
            }

            yield break;
        }

        if (sameAxis)
        {
            foreach (Path path in UPaths(from, to, width, maxLength))
            {
                yield return path;
            }

            foreach (Path path in WrapPaths(from, to, width, maxLength))
            {
                yield return path;
            }

            yield break;
        }

        if (facing)
        {
            Path? l = LPath(from, to, width);
            if (l.HasValue)
            {
                yield return l.Value;
            }

            Path? lAtB = LPathAtB(from, to, width);
            if (lAtB.HasValue)
            {
                yield return lAtB.Value;
            }

            foreach (Path path in LDetourPaths(from, to, width, maxLength))
            {
                yield return path;
            }

            yield break;
        }

        foreach (Path path in AroundCornerPaths(from, to, width, maxLength))
        {
            yield return path;
        }
    }

    private static Path? LPath(WorldPort from, WorldPort to, int width)
    {
        int fromAxis = Axis(from.Face);
        Vector3I corner = fromAxis == 0
            ? new Vector3I(to.AnchorCells.X, from.AnchorCells.Y, from.AnchorCells.Z)
            : new Vector3I(from.AnchorCells.X, from.AnchorCells.Y, to.AnchorCells.Z);
        Vector3I[] points = { from.AnchorCells, corner, to.AnchorCells };
        List<(Vector3I Origin, Vector3I Size)> boxes = BuildBoxes(points, width);
        if (boxes.Count == 0)
        {
            return null;
        }

        return new Path(boxes, PolylineLength(points), FirstCornerDistance(from.AnchorCells, corner, fromAxis));
    }

    private static Path? LPathAtB(WorldPort from, WorldPort to, int width)
    {
        int fromAxis = Axis(from.Face);
        Vector3I corner = fromAxis == 0
            ? new Vector3I(from.AnchorCells.X, from.AnchorCells.Y, to.AnchorCells.Z)
            : new Vector3I(to.AnchorCells.X, from.AnchorCells.Y, from.AnchorCells.Z);
        Vector3I[] points = { from.AnchorCells, corner, to.AnchorCells };
        List<(Vector3I Origin, Vector3I Size)> boxes = BuildBoxes(points, width);
        return boxes.Count == 0 ? null : new Path(boxes, PolylineLength(points), 0);
    }

    private static IEnumerable<Path> WrapPaths(WorldPort from, WorldPort to, int width, int maxLength)
    {
        int axis = Axis(from.Face);
        int tangent = TangentAxis(from.Face);
        int exit = AxisValue(from.AnchorCells, axis) + Sign(from.Face) * width;
        int entry = AxisValue(to.AnchorCells, axis) + Sign(to.Face) * width;
        int lane = TangentValue(from.AnchorCells, tangent);

        // The crossing lane must clear the departure corridor, so the nearest usable
        // offset is one full corridor width off the port's own tangent coordinate.
        for (int offset = width; offset <= maxLength; offset++)
        {
            foreach (int sign in new[] { -1, 1 })
            {
                int laneCoordinate = lane + sign * offset;
                Vector3I exitPoint = SetAxis(from.AnchorCells, axis, exit);
                Vector3I laneStart = SetTangent(exitPoint, tangent, laneCoordinate);
                Vector3I laneEnd = SetAxis(SetTangent(to.AnchorCells, tangent, laneCoordinate), axis, entry);
                Vector3I entryPoint = SetAxis(to.AnchorCells, axis, entry);
                Vector3I[] points =
                {
                    from.AnchorCells, exitPoint, laneStart, laneEnd, entryPoint, to.AnchorCells,
                };
                List<(Vector3I Origin, Vector3I Size)> boxes = BuildBoxes(points, width);
                if (boxes.Count > 0)
                {
                    yield return new Path(boxes, PolylineLength(points), width);
                }
            }
        }
    }

    private static IEnumerable<Path> LDetourPaths(WorldPort from, WorldPort to, int width, int maxLength)
    {
        int fromAxis = Axis(from.Face);
        int elbow = AxisValue(to.AnchorCells, fromAxis);
        int direction = Sign(from.Face);
        for (int offset = 1; offset <= maxLength; offset++)
        {
            foreach (int sign in new[] { -1, 1 })
            {
                Path? path = PerpendicularDetour(from, to, width, elbow + sign * direction * offset, offset);
                if (path.HasValue)
                {
                    yield return path.Value;
                }
            }
        }
    }

    private static IEnumerable<Path> AroundCornerPaths(WorldPort from, WorldPort to, int width, int maxLength)
    {
        int fromAxis = Axis(from.Face);
        int start = AxisValue(from.AnchorCells, fromAxis);
        int direction = Sign(from.Face);
        for (int offset = 1; offset <= maxLength; offset++)
        {
            Path? path = PerpendicularDetour(from, to, width, start + direction * offset, offset);
            if (path.HasValue)
            {
                yield return path.Value;
            }
        }
    }

    private static Path? PerpendicularDetour(WorldPort from, WorldPort to, int width, int corner, int approachOffset)
    {
        int fromAxis = Axis(from.Face);
        int toAxis = Axis(to.Face);
        if (Math.Sign(corner - AxisValue(from.AnchorCells, fromAxis)) != Sign(from.Face))
        {
            return null;
        }

        int approach = AxisValue(to.AnchorCells, toAxis) + Sign(to.Face) * approachOffset;
        Vector3I first = SetAxis(from.AnchorCells, fromAxis, corner);
        Vector3I second = SetAxis(first, toAxis, approach);
        Vector3I third = SetAxis(second, fromAxis, AxisValue(to.AnchorCells, fromAxis));
        Vector3I[] points = { from.AnchorCells, first, second, third, to.AnchorCells };
        List<(Vector3I Origin, Vector3I Size)> boxes = BuildBoxes(points, width);
        return boxes.Count == 0 ? null : new Path(boxes, PolylineLength(points), approachOffset);
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
            Vector3I[] points =
            {
                from.AnchorCells, firstCorner, SetAxis(to.AnchorCells, axis, turn), to.AnchorCells,
            };
            List<(Vector3I Origin, Vector3I Size)> boxes = BuildBoxes(points, width);
            if (boxes.Count == 0)
            {
                continue;
            }

            yield return new Path(boxes, PolylineLength(points), offset);
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
        Vector3I[] detourPoints =
        {
            from.AnchorCells, first, firstDetour, secondDetour, secondDrop, to.AnchorCells,
        };
        List<(Vector3I Origin, Vector3I Size)> detour = BuildBoxes(detourPoints, width);
        if (detour.Count > 0)
        {
            yield return new Path(detour, PolylineLength(detourPoints), 1);
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
        Vector3I[] points = { from.AnchorCells, firstCorner, secondCorner, to.AnchorCells };
        List<(Vector3I Origin, Vector3I Size)> boxes = BuildBoxes(points, width);
        if (boxes.Count > 0)
        {
            yield return new Path(boxes, PolylineLength(points), Math.Abs(outside - aAxis));
        }
    }

    /// <summary>
    ///     Turns an axis-aligned polyline of junction points into one box per non-degenerate leg. Each
    ///     leg's axis is the coordinate the two points differ on, and legs whose endpoints coincide are
    ///     dropped so the surviving neighbours are the ones treated as adjacent.
    /// </summary>
    private static List<(Vector3I Origin, Vector3I Size)> BuildBoxes(IReadOnlyList<Vector3I> points, int width)
    {
        var legs = new List<(Vector3I Start, Vector3I End, int Axis)>(points.Count);
        for (int i = 1; i < points.Count; i++)
        {
            Vector3I start = points[i - 1];
            Vector3I end = points[i];
            if (start.X == end.X && start.Z == end.Z)
            {
                continue;
            }

            legs.Add((start, end, start.X != end.X ? 0 : 1));
        }

        var boxes = new List<(Vector3I Origin, Vector3I Size)>(legs.Count);
        for (int i = 0; i < legs.Count; i++)
        {
            boxes.Add(Segment(legs[i].Start, legs[i].End, legs[i].Axis, width, i > 0, i < legs.Count - 1));
        }

        return boxes;
    }

    private static (Vector3I Origin, Vector3I Size) Segment(
        Vector3I start,
        Vector3I end,
        int axis,
        int width,
        bool startIsJunction,
        bool endIsJunction)
    {
        int from = AxisValue(start, axis);
        int to = AxisValue(end, axis);
        int low = to > from ? from : to + 1;
        int high = to > from ? to - 1 : from;

        // A junction's corner is a width×width block anchored at the junction point, and BOTH legs
        // must run through all of it: abutting on the anchor line leaves the bend's outer corner
        // covered by neither box at width ≥ 2. The double cover is the contract — the renderer dedupes
        // the union and ConnectorGridGeometry.RegionBoxes trims the block onto the earlier segment.
        if (startIsJunction)
        {
            low = Math.Min(low, from);
            high = Math.Max(high, from + width - 1);
        }

        if (endIsJunction)
        {
            low = Math.Min(low, to);
            high = Math.Max(high, to + width - 1);
        }
        else
        {
            // The base interval is half-open toward the destination because the NEXT leg's junction
            // block re-covers the handover cell. The final leg has no next leg, so it closes on the
            // destination anchor — otherwise the port plane keeps no corridor cell and no mouth exists.
            low = Math.Min(low, to);
            high = Math.Max(high, to);
        }

        return axis == 0
            ? (new Vector3I(low, start.Y, start.Z), new Vector3I(high - low + 1, 1, width))
            : (new Vector3I(start.X, start.Y, low), new Vector3I(width, 1, high - low + 1));
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

    // Measured on the polyline rather than the emitted boxes: a box carries its junction corner
    // blocks, so summing box extents would charge every bend twice against the length cap. The result
    // is junction-to-junction distance — one cell short of the run the boxes actually cover.
    private static int PolylineLength(IReadOnlyList<Vector3I> points)
    {
        int length = 0;
        for (int i = 1; i < points.Count; i++)
        {
            length += Math.Abs(points[i].X - points[i - 1].X) + Math.Abs(points[i].Z - points[i - 1].Z);
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
        int tangent = TangentAxis(from.Face);
        return TangentValue(from.AnchorCells, tangent) == TangentValue(to.AnchorCells, tangent) &&
            from.WidthCells == width && to.WidthCells == width;
    }

    private static bool IsCardinal(PortFace face)
        => face is PortFace.XPos or PortFace.XNeg or PortFace.ZPos or PortFace.ZNeg;

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
