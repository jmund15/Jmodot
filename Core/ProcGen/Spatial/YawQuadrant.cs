namespace Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     A 90° rotation about the vertical (Y) axis — the only rotations a grid-quantized template
///     placement may take. The integer values 0–3 are the quarter-turn count and are load-bearing
///     for the parity/rotation arithmetic (e.g. <c>SpatialParity</c>'s Yaw90 component-swap orbit).
/// </summary>
public enum YawQuadrant
{
    Yaw0 = 0,
    Yaw90 = 1,
    Yaw180 = 2,
    Yaw270 = 3,
}
