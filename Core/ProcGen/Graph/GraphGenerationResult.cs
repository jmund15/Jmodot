namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;

/// <summary>
///     The outcome of one SINGLE-ATTEMPT floor-graph generation (stage 1's internal carrier since
///     P3b.5 — the published surface is <c>FloorGenerationResult</c>, and the attempt count is
///     pipeline-owned). A data-only carrier. <see cref="Succeeded" /> is true iff no
///     <see cref="Severity.Fatal" /> violation occurred (warnings are non-fatal); the generator
///     (P3a.6) owns that invariant — this type stores it as authored.
/// </summary>
internal readonly struct GraphGenerationResult
{
    public GraphGenerationResult(
        IFloorGraph? graph,
        IReadOnlyList<Violation> violations,
        bool succeeded)
    {
        this.Graph = graph;
        this.Violations = violations;
        this.Succeeded = succeeded;
    }

    /// <summary>The generated topology, or null on a fatal floor failure.</summary>
    public IFloorGraph? Graph { get; }

    /// <summary>All violations encountered (fatal and advisory).</summary>
    public IReadOnlyList<Violation> Violations { get; }

    /// <summary>True iff generation produced a usable result (no fatal violation).</summary>
    public bool Succeeded { get; }
}
