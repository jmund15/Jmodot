namespace Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     The quantized spatial face of a port: which footprint face it sits on, plus its integer
///     offset, width, and elevation along that face. The stage-2 embedder downcasts an
///     <see cref="Jmodot.Core.ProcGen.Graph.IGraphPort" /> to this to read geometry; the stage-1
///     kernel never sees it. This namespace is deliberately exempt from Jmodot's 2D/3D parity
///     convention — dungeon-floor embedding is grid-3D only, with no 2D mirror.
/// </summary>
public interface ISpatialPort
{
    /// <summary>The footprint face this port sits on. <see cref="PortFace.Unset" /> is rejected by validation.</summary>
    PortFace Face { get; }

    /// <summary>
    ///     Start of the port's run along the face, from the face's min corner, in cells. A port with slack
    ///     (<see cref="SpanCells" /> above <see cref="WidthCells" />) places its door somewhere in that run, so this
    ///     is where the run begins, not where the door sits.
    /// </summary>
    int OffsetCells { get; }

    /// <summary>Elevation in cells. Committed 0 in v1 (validation pins == 0); the field exists so vertical activation is additive.</summary>
    int ElevationCells { get; }

    /// <summary>
    ///     The widest door this port admits along the face, in cells. 0 = unbaked (rejected). Two ports are
    ///     compatible when their door-width ranges <c>[MinWidthCells, WidthCells]</c> intersect.
    /// </summary>
    int WidthCells { get; }

    /// <summary>
    ///     The narrowest door this port admits, in cells. Implementations return a value in <c>1..WidthCells</c>;
    ///     a minimum equal to the width leaves the door no width choice.
    /// </summary>
    int MinWidthCells { get; }

    /// <summary>
    ///     The run along the face, from <see cref="OffsetCells" />, where a door of up to <see cref="WidthCells" />
    ///     may open, in cells. Implementations return at least <see cref="WidthCells" />; a span equal to the width
    ///     leaves the door no slide. Distinct from a consuming game's own geometry record of a port's span: this is
    ///     the run a door may take, not that game's cell math.
    /// </summary>
    int SpanCells { get; }
}
