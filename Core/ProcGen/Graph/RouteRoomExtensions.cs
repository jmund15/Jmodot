namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>The one definition of a route room: a room every incident edge of which is a <see cref="EdgeProvenanceKind.Loop" /> edge.</summary>
public static class RouteRoomExtensions
{
    /// <summary>True when <paramref name="incidentEdges" /> is non-empty and every edge in it is a loop edge.</summary>
    public static bool IsRouteRoom(this IEnumerable<IGraphEdge> incidentEdges)
    {
        bool any = false;
        foreach (IGraphEdge edge in incidentEdges)
        {
            if (edge.Provenance.Kind != EdgeProvenanceKind.Loop)
            {
                return false;
            }

            any = true;
        }

        return any;
    }

    /// <summary>True when the node <paramref name="nodeId" /> of <paramref name="graph" /> is a route room.</summary>
    public static bool IsRouteRoom(this IFloorGraph graph, StringName nodeId)
        => graph.Edges.Where(e => e.From.Id == nodeId || e.To.Id == nodeId).IsRouteRoom();
}
