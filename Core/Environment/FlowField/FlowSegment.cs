namespace Jmodot.Core.Environment.FlowField;

/// <summary>
///     One spine point of a sparse flow-field chain (wind current, conveyor, river). A chain of these
///     replaces a dense vector grid: positions are explicit, so crossing streams keep separate identity
///     and sub-cell geometry costs nothing.
/// </summary>
public struct FlowSegment
{
    /// <summary>World-space spine point.</summary>
    public Vector3 Position;

    /// <summary>Normalized flow direction. Callers are expected to keep this unit-length.</summary>
    public Vector3 Direction;

    /// <summary>Influence radius around the spine point; beyond it this segment contributes nothing.</summary>
    public float Radius;

    /// <summary>Accumulated energy — drives both applied force magnitude and visual intensity.</summary>
    public float Energy;
}
