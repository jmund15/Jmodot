namespace Jmodot.Implementation.ProcGen.Graph;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Concrete <see cref="IFloorGraph" />. Validates terminals, builds directed-outgoing
///     adjacency, and computes metrics exactly once at construction (cached). Plain CLR —
///     the generator constructs it at runtime; it is never .tres-authored.
/// </summary>
public sealed class FloorGraph : IFloorGraph
{
    private static readonly IReadOnlyList<IGraphEdge> EmptyEdges = Array.Empty<IGraphEdge>();

    private readonly Dictionary<StringName, List<IGraphEdge>> _forwardAdjacency;
    private readonly IGraphMetrics _metrics;

    public FloorGraph(
        IReadOnlyList<IGraphNode> nodes,
        IReadOnlyList<IGraphEdge> edges,
        IGraphNode source,
        IGraphNode sink)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sink);

        foreach (var edge in edges)
        {
            if (edge.Provenance.Kind == EdgeProvenanceKind.Unset)
            {
                throw new ArgumentException(
                    $"Edge {edge.From.Id}:{edge.FromPort} -> {edge.To.Id}:{edge.ToPort} carries Unset provenance; every edge must be stamped by its generation pass before the graph is built.",
                    nameof(edges));
            }
        }

        this.Nodes = nodes;
        this.Edges = edges;
        this.Source = source;
        this.Sink = sink;
        this._forwardAdjacency = GraphAnalyzer.BuildForwardAdjacency(edges);
        this._metrics = new GraphAnalyzer(nodes, edges, source, sink).Compute();
    }

    public IReadOnlyList<IGraphNode> Nodes { get; }

    public IReadOnlyList<IGraphEdge> Edges { get; }

    public IGraphNode Source { get; }

    public IGraphNode Sink { get; }

    public IReadOnlyList<IGraphEdge> EdgesFrom(IGraphNode node) =>
        this._forwardAdjacency.TryGetValue(node.Id, out var list) ? list : EmptyEdges;

    public IGraphMetrics Metrics => this._metrics;
}
