namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;

/// <summary>
///     Precomputed graph-theory metrics over a floor graph, all direction-aware
///     (Source → Sink). Computed once at graph construction and cached.
///     <para>
///         Extensibility note: adding a member here is a binary-breaking change for every
///         implementer — additions are bumped deliberately, not appended freely. If a future
///         Part needs more metrics, prefer a separate capability interface (e.g.
///         <c>IGraphFlowMetrics</c>) that a floor graph can additionally expose.
///     </para>
///     <para>
///         <see cref="CriticalEdges" /> (directed dominators) and <see cref="ArticulationNodes" />
///         (undirected cut-vertices) are <b>distinct</b> sets computed on different projections —
///         do not assume they align.
///     </para>
/// </summary>
public interface IGraphMetrics
{
    /// <summary>
    ///     True if <paramref name="node" /> is a dead end (degree-1 leaf) and is not a terminal
    ///     (Source/Sink are never dead ends).
    /// </summary>
    bool IsDeadEnd(IGraphNode node);

    /// <summary>
    ///     Shortest directed hop count from Source to <paramref name="node" />, or
    ///     <c>-1</c> if unreachable.
    /// </summary>
    int DistanceFromSource(IGraphNode node);

    /// <summary>
    ///     Shortest directed hop count from <paramref name="node" /> to Sink, or
    ///     <c>-1</c> if Sink is unreachable from the node.
    /// </summary>
    int DistanceToSink(IGraphNode node);

    /// <summary>
    ///     True if <paramref name="node" /> is reachable from Source traversing ONLY un-gated
    ///     edges (gated edges are treated as impassable). A node reachable only via a gated
    ///     detour returns false.
    /// </summary>
    bool IsReachableFromSourceWithoutGates(IGraphNode node);

    /// <summary>
    ///     Edges whose removal disconnects Sink from Source — the directed dominator-derived
    ///     cut edges. Empty if Sink is already unreachable from Source.
    ///     NOT the backbone/spine — for which generation pass laid an edge, use
    ///     <see cref="IGraphEdge.Provenance" />.
    /// </summary>
    IReadOnlyList<IGraphEdge> CriticalEdges { get; }

    /// <summary>
    ///     Cut vertices of the underlying undirected projection (whose removal raises the
    ///     undirected component count). Distinct from <see cref="CriticalEdges" />.
    /// </summary>
    IReadOnlyList<IGraphNode> ArticulationNodes { get; }
}
