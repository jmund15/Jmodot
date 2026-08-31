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

    private static readonly IReadOnlyList<ConnectorRealization> EmptyConnectors = new List<ConnectorRealization>();

    private static readonly IReadOnlyList<Violation> EmptyViolations = new List<Violation>();

    private readonly IReadOnlyDictionary<StringName, CellPlacement>? _layout;
    private readonly IReadOnlyList<DoorwayPose>? _doorways;
    private readonly IReadOnlyList<Violation>? _violations;
    private readonly IReadOnlyList<ConnectorRealization>? _connectors;
    private readonly bool _usedProgressiveEmbed;

    private FloorGenerationResult(
        IFloorGraph? graph,
        IReadOnlyDictionary<StringName, CellPlacement>? layout,
        IReadOnlyList<DoorwayPose>? doorways,
        IReadOnlyList<ConnectorRealization>? connectors,
        int attempts,
        IReadOnlyList<Violation>? violations,
        bool succeeded,
        bool usedProgressiveEmbed)
    {
        this.Graph = graph;
        this._layout = layout;
        this._doorways = doorways;
        this._connectors = connectors;
        this.Attempts = attempts;
        this._violations = violations;
        this.Succeeded = succeeded;
        this._usedProgressiveEmbed = usedProgressiveEmbed;
    }

    public IFloorGraph? Graph { get; }

    public IReadOnlyDictionary<StringName, CellPlacement> Layout => this._layout ?? EmptyLayout;

    public IReadOnlyList<DoorwayPose> Doorways => this._doorways ?? EmptyDoorways;

    /// <summary>Published loop connectors, empty when none were synthesized.</summary>
    public IReadOnlyList<ConnectorRealization> Connectors => this._connectors ?? EmptyConnectors;

    public int Attempts { get; }

    /// <summary>Whether the pipeline used progressive embedding for this result.</summary>
    public bool UsedProgressiveEmbed => this._usedProgressiveEmbed;

    public IReadOnlyList<Violation> Violations => this._violations ?? EmptyViolations;

    public bool Succeeded { get; }

    public static FloorGenerationResult Success(
        IFloorGraph graph,
        IReadOnlyDictionary<StringName, CellPlacement> layout,
        IReadOnlyList<DoorwayPose> doorways,
        int attempts,
        IReadOnlyList<Violation> violations,
        IReadOnlyList<ConnectorRealization>? connectors = null,
        bool usedProgressiveEmbed = false)
    {
        return new FloorGenerationResult(graph, layout, doorways, connectors, attempts, violations, succeeded: true, usedProgressiveEmbed);
    }

    public static FloorGenerationResult Failure(
        int attempts,
        IReadOnlyList<Violation> violations,
        bool usedProgressiveEmbed = false)
    {
        return new FloorGenerationResult(null, null, null, null, attempts, violations, succeeded: false, usedProgressiveEmbed);
    }
}
