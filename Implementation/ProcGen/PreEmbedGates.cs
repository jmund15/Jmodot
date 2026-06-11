namespace Jmodot.Implementation.ProcGen;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     Cheap, deterministic necessary-condition gates the pipeline runs on a finished topology
///     BEFORE any pose search (design-se §1): (b) per-node bipartite matching of incident edges
///     against template ports under <see cref="PortCompatibility.Matches" /> — stage 1 binds by
///     TYPE only, so width-incompatible neighborhoods pass generation and must be caught here;
///     (c) envelope arithmetic (footprint-area sum and per-template dimension fit). Gate (a)
///     closure-parity runs inside the embedder's own pre-search check (P3b.4) — a typed
///     <see cref="EmbedFailureCause.ClosureParity" /> return IS that gate firing. Non-spatial
///     templates are skipped; the embedder rejects them loudly at its own entry.
/// </summary>
internal static class PreEmbedGates
{
    public static bool Check(
        IFloorGraph topology,
        GeometryEnvelope envelope,
        out EmbedFailureCause cause,
        out StringName? failingNodeId)
    {
        Vector3I size = envelope.SizeCells;
        int envMax = Math.Max(size.X, size.Z);
        int envMin = Math.Min(size.X, size.Z);
        long envelopeArea = (long)size.X * size.Z;

        long areaSum = 0;
        foreach (IGraphNode node in topology.Nodes)
        {
            if (node.Template is not ISpatialNodeTemplate spatial)
            {
                continue;
            }

            Vector3I f = spatial.FootprintCells;
            areaSum += (long)f.X * f.Z;
            int footMax = Math.Max(f.X, f.Z);
            int footMin = Math.Min(f.X, f.Z);
            if (f.Y > size.Y || footMax > envMax || footMin > envMin)
            {
                cause = EmbedFailureCause.SpaceTight;
                failingNodeId = node.Id;
                return false;
            }
        }

        if (areaSum > envelopeArea)
        {
            cause = EmbedFailureCause.SpaceTight;
            failingNodeId = topology.Source.Id;
            return false;
        }

        foreach (IGraphNode node in topology.Nodes)
        {
            if (node.Template is not ISpatialNodeTemplate)
            {
                continue;
            }

            if (!NodePortsCoverIncidentEdges(topology, node))
            {
                cause = EmbedFailureCause.NoBinding;
                failingNodeId = node.Id;
                return false;
            }
        }

        cause = default;
        failingNodeId = null;
        return true;
    }

    // Maximum bipartite matching (augmenting DFS) between the node's incident edges and its
    // spatial ports; an edge admits a port iff the NEIGHBOR also owns a compatible spatial port.
    // Poly-time necessary condition — the embedder's search decides actual assignments.
    private static bool NodePortsCoverIncidentEdges(IFloorGraph topology, IGraphNode node)
    {
        var incident = topology.Edges
            .Where(e => ReferenceEquals(e.From, node) || ReferenceEquals(e.To, node))
            .ToList();
        if (incident.Count == 0)
        {
            return true;
        }

        var ports = node.Template.Ports.Where(p => p is ISpatialPort).ToList();
        if (ports.Count < incident.Count)
        {
            return false;
        }

        var admissible = new bool[incident.Count, ports.Count];
        for (int e = 0; e < incident.Count; e++)
        {
            IGraphNode neighbor = ReferenceEquals(incident[e].From, node) ? incident[e].To : incident[e].From;
            var neighborPorts = neighbor.Template.Ports.Where(p => p is ISpatialPort).ToList();
            for (int p = 0; p < ports.Count; p++)
            {
                admissible[e, p] = neighborPorts.Any(q => PortCompatibility.Matches(ports[p], q));
            }
        }

        var portAssignedTo = new int[ports.Count];
        Array.Fill(portAssignedTo, -1);
        for (int e = 0; e < incident.Count; e++)
        {
            var visited = new bool[ports.Count];
            if (!TryAssign(e, admissible, portAssignedTo, visited))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryAssign(int edge, bool[,] admissible, int[] portAssignedTo, bool[] visited)
    {
        for (int p = 0; p < portAssignedTo.Length; p++)
        {
            if (!admissible[edge, p] || visited[p])
            {
                continue;
            }

            visited[p] = true;
            if (portAssignedTo[p] == -1 || TryAssign(portAssignedTo[p], admissible, portAssignedTo, visited))
            {
                portAssignedTo[p] = edge;
                return true;
            }
        }

        return false;
    }
}
