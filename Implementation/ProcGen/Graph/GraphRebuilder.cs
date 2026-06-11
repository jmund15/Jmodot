namespace Jmodot.Implementation.ProcGen.Graph;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     Rebuilds a finished topology as a fresh immutable <see cref="FloorGraph" /> carrying the
///     embedder's re-bound ports (design-se §5). The doorway list is the binding record — one
///     <see cref="DoorwayPose" /> per edge, index-aligned with <c>topology.Edges</c> (the embedder
///     emits doorways by iterating that exact list). Node ids, templates, source/sink, gating,
///     traversal, and provenance pass through unchanged; only the port columns may differ.
/// </summary>
internal static class GraphRebuilder
{
    public static FloorGraph Rebuild(IFloorGraph topology, IReadOnlyList<DoorwayPose> doorways)
    {
        if (topology == null)
        {
            throw new ArgumentNullException(nameof(topology));
        }

        if (doorways == null)
        {
            throw new ArgumentNullException(nameof(doorways));
        }

        if (doorways.Count != topology.Edges.Count)
        {
            throw new ArgumentException(
                $"Binding record out of alignment: {doorways.Count} doorways for {topology.Edges.Count} edges.",
                nameof(doorways));
        }

        var partial = new PartialGraph();
        var nodesById = new Dictionary<StringName, GraphNode>();
        foreach (IGraphNode node in topology.Nodes)
        {
            nodesById[node.Id] = partial.AddNode(node.Id, node.Template);
        }

        for (int i = 0; i < topology.Edges.Count; i++)
        {
            IGraphEdge edge = topology.Edges[i];
            DoorwayPose doorway = doorways[i];
            if (doorway.FromNodeId != edge.From.Id || doorway.ToNodeId != edge.To.Id)
            {
                throw new ArgumentException(
                    $"Doorway {i} binds {doorway.FromNodeId} -> {doorway.ToNodeId} but edge {i} is {edge.From.Id} -> {edge.To.Id}; the binding record must stay index-aligned with topology.Edges.",
                    nameof(doorways));
            }

            GraphNode from = nodesById[edge.From.Id];
            GraphNode to = nodesById[edge.To.Id];
            partial.Connect(
                from, PortByName(from, doorway.FromPort),
                to, PortByName(to, doorway.ToPort),
                edge.IsGated, edge.Traversal, edge.Provenance);
        }

        partial.SetSource(nodesById[topology.Source.Id]);
        partial.SetSink(nodesById[topology.Sink.Id]);
        return partial.ToFloorGraph();
    }

    private static IGraphPort PortByName(GraphNode node, StringName portName)
    {
        IGraphPort? port = node.Template.Ports.FirstOrDefault(p => p.Name == portName);
        if (port == null)
        {
            throw new ArgumentException(
                $"Node '{node.Id}' template '{node.Template.TemplateId}' has no port named '{portName}' to re-bind.");
        }

        return port;
    }
}
