namespace Jmodot.Implementation.ProcGen.Graph;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
/// Endpoint queries over the game-agnostic <see cref="IFloorGraph"/> topology. Edges carry no id —
/// endpoints are identified by node <see cref="IGraphNode.Id"/> — so traversal resolves "the node
/// across this edge from here" by matching that id against the edge's <see cref="IGraphEdge.From"/>
/// / <see cref="IGraphEdge.To"/>. Canonical home for the endpoint-resolution logic that the grid
/// embedder previously duplicated.
/// </summary>
public static class FloorGraphExtensions
{
    /// <summary>
    /// The node on the opposite end of <paramref name="edge"/> from <paramref name="fromNodeId"/>,
    /// or <c>null</c> when <paramref name="fromNodeId"/> is not one of the edge's endpoints.
    /// </summary>
    public static IGraphNode? NodeAcrossEdge(this IGraphEdge edge, StringName fromNodeId)
    {
        if (edge.From.Id == fromNodeId) { return edge.To; }
        if (edge.To.Id == fromNodeId) { return edge.From; }
        return null;
    }

    /// <summary>
    /// The distinct nodes reachable from <paramref name="node"/> across its directed-outgoing edges
    /// (<see cref="IFloorGraph.EdgesFrom"/>, which includes bidirectional edges arriving at the node).
    /// </summary>
    public static IEnumerable<IGraphNode> Neighbors(this IFloorGraph graph, IGraphNode node)
    {
        foreach (var edge in graph.EdgesFrom(node))
        {
            var other = edge.NodeAcrossEdge(node.Id);
            if (other != null) { yield return other; }
        }
    }
}
