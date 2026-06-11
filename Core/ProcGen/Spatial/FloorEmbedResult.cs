namespace Jmodot.Core.ProcGen.Spatial;

using System.Collections.Generic;
using Godot;

/// <summary>
///     The embedder's outcome (design-se §4). On success: one <see cref="CellPlacement" /> per node
///     and one <see cref="DoorwayPose" /> per edge — the doorway list doubles as the binding record
///     (final port pair per edge). On failure: a typed cause + the failing node id; collections are
///     empty, never null.
/// </summary>
public readonly struct FloorEmbedResult
{
    private static readonly IReadOnlyDictionary<StringName, CellPlacement> EmptyLayout =
        new Dictionary<StringName, CellPlacement>();

    private static readonly IReadOnlyList<DoorwayPose> EmptyDoorways = new List<DoorwayPose>();

    private FloorEmbedResult(
        bool succeeded,
        IReadOnlyDictionary<StringName, CellPlacement> layout,
        IReadOnlyList<DoorwayPose> doorways,
        EmbedFailureCause? failureCause,
        StringName? failingNodeId)
    {
        this.Succeeded = succeeded;
        this.Layout = layout;
        this.Doorways = doorways;
        this.FailureCause = failureCause;
        this.FailingNodeId = failingNodeId;
    }

    public bool Succeeded { get; }

    public IReadOnlyDictionary<StringName, CellPlacement> Layout { get; }

    public IReadOnlyList<DoorwayPose> Doorways { get; }

    public EmbedFailureCause? FailureCause { get; }

    public StringName? FailingNodeId { get; }

    public static FloorEmbedResult Success(
        IReadOnlyDictionary<StringName, CellPlacement> layout,
        IReadOnlyList<DoorwayPose> doorways)
    {
        return new FloorEmbedResult(true, layout, doorways, null, null);
    }

    public static FloorEmbedResult Failure(EmbedFailureCause cause, StringName failingNodeId)
    {
        return new FloorEmbedResult(false, EmptyLayout, EmptyDoorways, cause, failingNodeId);
    }
}
