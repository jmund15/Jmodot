namespace Jmodot.Core.ProcGen.Spatial;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     The pipeline's published result (design-se §5): the REBUILT graph (re-bound ports), the
///     cell layout keyed by node id (never <see cref="IGraphNode" /> reference — rebuild creates
///     new instances, so reference keys would dangle), doorway poses (one per edge), the
///     pipeline-owned attempt count, and the aggregated violations. Fails closed: a
///     default-constructed instance reports failure with empty, never-null collections.
/// </summary>
public readonly struct FloorGenerationResult
{
    private static readonly IReadOnlyDictionary<StringName, CellPlacement> EmptyLayout =
        new Dictionary<StringName, CellPlacement>();

    private static readonly IReadOnlyList<DoorwayPose> EmptyDoorways = new List<DoorwayPose>();

    private static readonly IReadOnlyList<Violation> EmptyViolations = new List<Violation>();

    private readonly IReadOnlyDictionary<StringName, CellPlacement>? _layout;
    private readonly IReadOnlyList<DoorwayPose>? _doorways;
    private readonly IReadOnlyList<Violation>? _violations;

    private FloorGenerationResult(
        IFloorGraph? graph,
        IReadOnlyDictionary<StringName, CellPlacement>? layout,
        IReadOnlyList<DoorwayPose>? doorways,
        int attempts,
        IReadOnlyList<Violation>? violations,
        bool succeeded)
    {
        this.Graph = graph;
        this._layout = layout;
        this._doorways = doorways;
        this.Attempts = attempts;
        this._violations = violations;
        this.Succeeded = succeeded;
    }

    public IFloorGraph? Graph { get; }

    public IReadOnlyDictionary<StringName, CellPlacement> Layout => this._layout ?? EmptyLayout;

    public IReadOnlyList<DoorwayPose> Doorways => this._doorways ?? EmptyDoorways;

    public int Attempts { get; }

    public IReadOnlyList<Violation> Violations => this._violations ?? EmptyViolations;

    public bool Succeeded { get; }

    public static FloorGenerationResult Success(
        IFloorGraph graph,
        IReadOnlyDictionary<StringName, CellPlacement> layout,
        IReadOnlyList<DoorwayPose> doorways,
        int attempts,
        IReadOnlyList<Violation> violations)
    {
        return new FloorGenerationResult(graph, layout, doorways, attempts, violations, succeeded: true);
    }

    public static FloorGenerationResult Failure(int attempts, IReadOnlyList<Violation> violations)
    {
        return new FloorGenerationResult(null, null, null, attempts, violations, succeeded: false);
    }
}
