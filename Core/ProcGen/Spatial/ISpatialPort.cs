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

    /// <summary>Offset along the face from the face's min corner, in cells.</summary>
    int OffsetCells { get; }

    /// <summary>Elevation in cells. Committed 0 in v1 (validation pins == 0); the field exists so vertical activation is additive.</summary>
    int ElevationCells { get; }

    /// <summary>Port width along the face, in cells. 0 = unbaked (rejected); v1 compatibility is exact-match.</summary>
    int WidthCells { get; }
}
