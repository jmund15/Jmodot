namespace Jmodot.Core.ProcGen.Spatial;

using Godot;

/// <summary>
///     A placed cell-grid occupancy: the template's min-corner origin, the yaw it is rotated by, and
///     its (pre-yaw) cell extent. Produced by the embedder (P3b.4/P3b.5); plain CLR data, never
///     .tres-authored.
/// </summary>
public readonly record struct CellPlacement(Vector3I OriginCells, YawQuadrant Yaw, Vector3I SizeCells);
