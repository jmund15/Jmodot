namespace Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     One door window on a port: where along the port's face the door opens and how wide, in the owning
///     template's local frame. Translation- and yaw-free, so a binding stays valid wherever its room is posed.
/// </summary>
/// <param name="OffsetCells">Offset along the face from the face's min corner, in cells. Lies inside the port's span.</param>
/// <param name="WidthCells">Door width along the face, in cells.</param>
public readonly record struct PortOpening(int OffsetCells, int WidthCells);
