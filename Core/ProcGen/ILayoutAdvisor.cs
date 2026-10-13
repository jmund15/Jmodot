namespace Jmodot.Core.ProcGen;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     Optional GEOMETRY seam the topology generator may consult while it builds a floor — the
///     generator-owned port behind which a spatial embedder hides (dependency inversion, sibling of the
///     injected <c>Func&lt;int,IRng&gt;</c>). When present, the generator validates each loop/branch
///     against the REAL grid before committing it, instead of guessing from graph metrics and re-rolling
///     the whole topology on an embed miss; when absent (null), the generator falls back to its
///     graph-only heuristics and runs fully standalone (the two-stage decoupling is preserved).
///     <para>
///         Lifecycle: an advisor is created from the committed backbone (its spine is embedded + FROZEN
///         on creation). The generator asks geometric questions (<see cref="BridgeEstimate" />,
///         <see cref="NodesWithFreeSpur" />), then authoritatively reserves a decoration with
///         <see cref="TryCommitSubgraph" /> (trial-embed; committed only if it actually closes on the
///         grid). The pipeline emits the final layout with <see cref="BuildResult" />, reusing every
///         frozen pose so the generator's geometric decisions are binding.
///     </para>
/// </summary>
public interface ILayoutAdvisor
{
    /// <summary>Shortest corridor length (floor XZ cells) between a PLACED room's port and another's, or null if
    /// either room is not yet committed or no corridor joins the two doors. Orders anchor pairs by the doors the
    /// route will leave and rejoin through.</summary>
    int? BridgeEstimate(StringName a, StringName aPort, StringName b, StringName bPort);

    /// <summary>Committed nodes still exposing an unbound port — candidate branch/loop attach points.
    /// A coarse pre-filter; <see cref="TryCommitSubgraph" /> is the authoritative feasibility gate.</summary>
    IReadOnlyList<StringName> NodesWithFreeSpur();

    /// <summary>Trial-embeds the not-yet-placed nodes of <paramref name="graphSoFar" /> onto the frozen
    /// state; commits (and freezes) them on success, no-ops on failure. Must share node/edge instances
    /// with the backbone. Returns whether the decoration actually embeds.</summary>
    bool TryCommitSubgraph(IFloorGraph graphSoFar);

    /// <summary>Emits the final layout from the accumulated frozen poses (any uncommitted node is placed
    /// now). Pipeline-facing — the generator never calls this.</summary>
    FloorEmbedResult BuildResult(IFloorGraph fullGraph);

    /// <summary>The embed failure of the backbone this advisor was opened on, or null when every backbone room
    /// placed. Set when the advisor opens: non-null means the backbone did not fully place and every
    /// <see cref="TryCommitSubgraph" /> will refuse, so a caller that reads it can stop the attempt before
    /// trialing any decoration.</summary>
    FloorEmbedResult? BackboneFailure { get; }
}
