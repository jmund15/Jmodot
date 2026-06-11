namespace Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     The cardinal face of an axis-aligned cell footprint that a port sits on, in template-local
///     space. <see cref="Unset" /> is the invalid-at-zero sentinel (mirrors the
///     <c>EdgeProvenanceKind.Unset</c> convention) — validation rejects it; a baked port always
///     carries a concrete face.
/// </summary>
public enum PortFace
{
    Unset = 0,
    XPos,
    XNeg,
    ZPos,
    ZNeg,
}
