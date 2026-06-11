namespace Jmodot.Core.ProcGen.Spatial;

using Godot;

/// <summary>
///     The resolved spatial pose of a doorway between two placed nodes: the connected node and port
///     ids, the shared-face min-corner origin, the face the doorway lies on, and its width in cells.
///     Produced by the embedder (P3b.5); plain CLR data, never .tres-authored.
/// </summary>
public readonly record struct DoorwayPose(
    StringName FromNodeId, StringName ToNodeId,
    StringName FromPort, StringName ToPort,
    Vector3I SharedFaceOriginCells, PortFace Face, int WidthCells);
