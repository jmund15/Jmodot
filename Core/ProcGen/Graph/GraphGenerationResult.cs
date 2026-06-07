namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;

/// <summary>
///     The outcome of one floor-graph generation. A data-only carrier. <see cref="Succeeded" /> is
///     true iff no <see cref="Severity.Fatal" /> violation occurred (warnings are non-fatal); the
///     generator (P3a.6) owns that invariant — this Part stores it as authored.
///     <para>
///     <see cref="Layout" /> keys on the <see cref="IGraphNode" /> interface (reading only
///     <c>Id</c> + <c>Template</c>), keeping this type Core-pure (no <c>Implementation</c> reference)
///     and consistent with <see cref="Graph" /> being an <see cref="IFloorGraph" />.
///     </para>
/// </summary>
public readonly struct GraphGenerationResult
{
    public GraphGenerationResult(
        IFloorGraph? graph,
        IReadOnlyDictionary<IGraphNode, ReservedRegion> layout,
        int attempts,
        IReadOnlyList<Violation> violations,
        bool succeeded)
    {
        this.Graph = graph;
        this.Layout = layout;
        this.Attempts = attempts;
        this.Violations = violations;
        this.Succeeded = succeeded;
    }

    /// <summary>The generated topology, or null on a fatal floor failure.</summary>
    public IFloorGraph? Graph { get; }

    /// <summary>Per-node reserved region (the spatial layout), keyed by node.</summary>
    public IReadOnlyDictionary<IGraphNode, ReservedRegion> Layout { get; }

    /// <summary>How many floor attempts were made before this result.</summary>
    public int Attempts { get; }

    /// <summary>All violations encountered (fatal and advisory).</summary>
    public IReadOnlyList<Violation> Violations { get; }

    /// <summary>True iff generation produced a usable result (no fatal violation).</summary>
    public bool Succeeded { get; }
}
