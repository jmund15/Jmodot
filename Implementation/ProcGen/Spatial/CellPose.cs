namespace Jmodot.Implementation.ProcGen.Spatial;

using Godot;
using Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     Candidate pose under evaluation during the embed search: (origin, yaw) only. SizeCells is
///     intentionally absent — it is queried from <see cref="ISpatialNodeTemplate.FootprintCells" />
///     at search time and never carried through the frontier. Committed placements are represented
///     by <see cref="CellPlacement" />.
/// </summary>
internal readonly record struct CellPose(Vector3I Origin, YawQuadrant Yaw);
