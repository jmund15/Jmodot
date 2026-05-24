namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;

/// <summary>
///     A pure, game-agnostic floor topology: nodes, edges, the Source/Sink terminals, and a
///     precomputed <see cref="IGraphMetrics" /> surface. Consumers (encounter placement,
///     destruction, spatial sequencing) read this output; they do not construct it.
///     Coordinate-free — no positions live on the graph.
/// </summary>
public interface IFloorGraph
{
    /// <summary>All nodes in the graph.</summary>
    IReadOnlyList<IGraphNode> Nodes { get; }

    /// <summary>All edges in the graph.</summary>
    IReadOnlyList<IGraphEdge> Edges { get; }

    /// <summary>The entry terminal.</summary>
    IGraphNode Source { get; }

    /// <summary>The exit terminal.</summary>
    IGraphNode Sink { get; }

    /// <summary>
    ///     Directed-outgoing edges of <paramref name="node" />: edges where
    ///     <c>edge.From == node</c>, plus bidirectional edges where <c>edge.To == node</c>.
    /// </summary>
    IReadOnlyList<IGraphEdge> EdgesFrom(IGraphNode node);

    /// <summary>Precomputed metrics over this graph (computed once, cached).</summary>
    IGraphMetrics Metrics { get; }
}
