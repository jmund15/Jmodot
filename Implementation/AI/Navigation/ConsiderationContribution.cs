namespace Jmodot.Implementation.AI.Navigation;

using System;

/// <summary>
/// One consideration's per-bin contribution to a <see cref="SteeringContextMap"/> for a single
/// frame, captured by <see cref="DebugSteeringRecorder"/> via snapshot/diff of the map's channels
/// before and after that consideration's Evaluate call. Instances are pooled and reused across
/// frames to avoid steady-state allocation, so a reference is valid only until the next
/// <see cref="DebugSteeringRecorder.BeginFrame"/>.
/// </summary>
public sealed class ConsiderationContribution
{
    /// <summary>The consideration's ResourceName, or its CLR type name when unnamed.</summary>
    public string Source { get; internal set; } = string.Empty;

    /// <summary>Per-bin interest this consideration added this frame (index-aligned to the map's Bins).</summary>
    public float[] InterestDelta { get; internal set; } = Array.Empty<float>();

    /// <summary>Per-bin danger this consideration added this frame.</summary>
    public float[] DangerDelta { get; internal set; } = Array.Empty<float>();

    /// <summary>Per-bin: true where this consideration newly raised the Hard mask this frame.</summary>
    public bool[] MaskAdded { get; internal set; } = Array.Empty<bool>();
}
