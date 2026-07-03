namespace Jmodot.Implementation.ProcGen.Spatial;

using Godot;
using Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     Public seam over the internal <see cref="SpatialPoseMath" /> abutment arithmetic. Lets a
///     consuming game compute an abutting node's grid origin while passing only public contract types
///     (<see cref="ISpatialPort" />, <see cref="Vector3I" />, <see cref="YawQuadrant" />) — the
///     internal <c>WorldPort</c>/<c>CellPose</c> value types never cross the boundary, so the
///     framework-boundary rule (Jmodot never references the consuming project) is preserved while the
///     internal pose math stays encapsulated.
/// </summary>
public static class FloorAbutmentSolver
{
    /// <summary>
    ///     The grid origin at which the "to" node (footprint <paramref name="toFootprintCells" />,
    ///     rotation <paramref name="toYaw" />) must sit so that its <paramref name="toPort" /> abuts
    ///     the <paramref name="fromPort" /> of an already-placed "from" node (at
    ///     <paramref name="fromOrigin" /> / <paramref name="fromYaw" />). Returns null when the ports
    ///     are incompatible — non-opposing world faces or mismatched span widths.
    /// </summary>
    public static Vector3I? PlaceNext(
        Vector3I fromOrigin, YawQuadrant fromYaw, Vector3I fromFootprintCells, ISpatialPort fromPort,
        ISpatialPort toPort, Vector3I toFootprintCells, YawQuadrant toYaw)
    {
        var fromWorld = SpatialPoseMath.WorldPortOf(new CellPose(fromOrigin, fromYaw), fromFootprintCells, fromPort);
        return SpatialPoseMath.SolveAbutment(fromWorld, toPort, toFootprintCells, toYaw);
    }
}
