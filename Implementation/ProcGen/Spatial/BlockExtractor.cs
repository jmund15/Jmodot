namespace Jmodot.Implementation.ProcGen.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     One biconnected block of the topology: every cycle it contains must be embedded
///     simultaneously (loops share spine segments; theta and interleaved shapes are legal).
///     Node/edge order follows topology insertion order, part of the determinism contract.
/// </summary>
internal sealed class GraphBlock
{
    internal GraphBlock(
        IReadOnlyList<IGraphNode> nodes,
        IReadOnlyList<IGraphEdge> edges,
        IReadOnlyList<IReadOnlyList<IGraphEdge>> cycles)
    {
        this.Nodes = nodes;
        this.Edges = edges;
        this.Cycles = cycles;
    }

    public IReadOnlyList<IGraphNode> Nodes { get; }

    public IReadOnlyList<IGraphEdge> Edges { get; }

    /// <summary>The block's constituent cycles (one per loop route), input to the parity pre-check.</summary>
    public IReadOnlyList<IReadOnlyList<IGraphEdge>> Cycles { get; }
}

/// <summary>
///     Block-cut decomposition of a finished topology: biconnected blocks first, tree remainder
///     (chains, branch leaves) after.
/// </summary>
internal sealed class BlockDecomposition
{
    internal BlockDecomposition(IReadOnlyList<GraphBlock> blocks, IReadOnlyList<IGraphEdge> treeEdges)
    {
        this.Blocks = blocks;
        this.TreeEdges = treeEdges;
    }

    public IReadOnlyList<GraphBlock> Blocks { get; }

    public IReadOnlyList<IGraphEdge> TreeEdges { get; }
}

/// <summary>
///     Provenance-walk block-cut extraction (design-se §4): each Loop route (edges sharing
///     <c>(Loop, RouteOrdinal)</c>) plus the unique non-Loop path between its anchors defines a
///     cycle; cycles merge into one biconnected block when they share an edge or two-plus vertices
///     (sharing only a cut vertex keeps them separate blocks). The non-Loop skeleton (spine +
///     branches) is a tree, so the anchor-to-anchor path is unique and the walk needs no general
///     cycle search — this is what edge provenance buys over a from-scratch Tarjan pass.
/// </summary>
internal static class BlockExtractor
{
    public static BlockDecomposition Extract(IFloorGraph topology)
    {
        var cycles = ExtractCycles(topology);
        var merged = MergeCycles(cycles);

        var blocks = new List<GraphBlock>(merged.Count);
        var edgesInBlocks = new HashSet<IGraphEdge>();
        foreach (List<List<IGraphEdge>> cycleGroup in merged)
        {
            var edgeSet = cycleGroup.SelectMany(c => c).ToHashSet();
            var nodeIds = new HashSet<StringName>();
            foreach (IGraphEdge edge in edgeSet)
            {
                nodeIds.Add(edge.From.Id);
                nodeIds.Add(edge.To.Id);
                edgesInBlocks.Add(edge);
            }

            var nodes = topology.Nodes.Where(n => nodeIds.Contains(n.Id)).ToList();
            var edges = topology.Edges.Where(edgeSet.Contains).ToList();
            var blockCycles = cycleGroup.Select(c => (IReadOnlyList<IGraphEdge>)c).ToList();
            blocks.Add(new GraphBlock(nodes, edges, blockCycles));
        }

        var treeEdges = topology.Edges.Where(e => !edgesInBlocks.Contains(e)).ToList();
        return new BlockDecomposition(blocks, treeEdges);
    }

    private static List<List<IGraphEdge>> ExtractCycles(IFloorGraph topology)
    {
        var routes = topology.Edges
            .Where(e => e.Provenance.Kind == EdgeProvenanceKind.Loop)
            .GroupBy(e => e.Provenance.RouteOrdinal)
            .OrderBy(g => g.Key);

        var cycles = new List<List<IGraphEdge>>();
        foreach (IGrouping<int, IGraphEdge> route in routes)
        {
            var routeEdges = route.ToList();
            (StringName anchorA, StringName anchorB) = RouteAnchors(routeEdges);
            var connectingPath = TreePath(topology, anchorA, anchorB);

            var cycle = new List<IGraphEdge>(routeEdges);
            cycle.AddRange(connectingPath);
            cycles.Add(cycle);
        }

        return cycles;
    }

    // internal (not private) so the anchor-extraction invariant is directly unit-testable.
    internal static (StringName A, StringName B) RouteAnchors(IReadOnlyList<IGraphEdge> routeEdges)
    {
        var incidence = new Dictionary<StringName, int>();
        foreach (IGraphEdge edge in routeEdges)
        {
            incidence[edge.From.Id] = incidence.GetValueOrDefault(edge.From.Id) + 1;
            incidence[edge.To.Id] = incidence.GetValueOrDefault(edge.To.Id) + 1;
        }

        // A well-formed Loop route is an open chain with exactly two degree-1 endpoints. A self-closed
        // or otherwise malformed route violates that invariant; fail loud with the offending count
        // rather than throwing an opaque IndexOutOfRangeException on anchors[0]/anchors[1].
        var anchors = incidence.Where(kv => kv.Value == 1).Select(kv => kv.Key).ToList();
        if (anchors.Count != 2)
        {
            throw new InvalidOperationException(
                $"RouteAnchors expected exactly two degree-1 endpoints in a Loop route but found {anchors.Count}: " +
                "the route is self-closed or malformed, indicating bad topology upstream of the embedder.");
        }

        return (anchors[0], anchors[1]);
    }

    private static List<IGraphEdge> TreePath(IFloorGraph topology, StringName from, StringName to)
    {
        var adjacency = new Dictionary<StringName, List<(StringName Neighbor, IGraphEdge Edge)>>();
        foreach (IGraphEdge edge in topology.Edges)
        {
            if (edge.Provenance.Kind == EdgeProvenanceKind.Loop)
            {
                continue;
            }

            AddAdjacency(adjacency, edge.From.Id, edge.To.Id, edge);
            AddAdjacency(adjacency, edge.To.Id, edge.From.Id, edge);
        }

        var cameBy = new Dictionary<StringName, IGraphEdge>();
        var cameFrom = new Dictionary<StringName, StringName>();
        var queue = new Queue<StringName>();
        var visited = new HashSet<StringName> { from };
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            StringName current = queue.Dequeue();
            if (current == to)
            {
                break;
            }

            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach ((StringName neighbor, IGraphEdge edge) in neighbors)
            {
                if (!visited.Add(neighbor))
                {
                    continue;
                }

                cameBy[neighbor] = edge;
                cameFrom[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }

        var path = new List<IGraphEdge>();
        StringName cursor = to;
        while (cursor != from)
        {
            if (!cameBy.TryGetValue(cursor, out IGraphEdge? edge))
            {
                throw new System.InvalidOperationException(
                    $"BlockExtractor: no non-Loop path connects route anchors '{from}' and '{to}' — "
                    + "loop routes must anchor on the spine/branch tree (generator invariant).");
            }

            path.Add(edge);
            cursor = cameFrom[cursor];
        }

        return path;
    }

    private static void AddAdjacency(
        Dictionary<StringName, List<(StringName, IGraphEdge)>> adjacency,
        StringName from,
        StringName to,
        IGraphEdge edge)
    {
        if (!adjacency.TryGetValue(from, out var list))
        {
            list = new List<(StringName, IGraphEdge)>();
            adjacency[from] = list;
        }

        list.Add((to, edge));
    }

    private static List<List<List<IGraphEdge>>> MergeCycles(List<List<IGraphEdge>> cycles)
    {
        int[] parent = Enumerable.Range(0, cycles.Count).ToArray();

        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        void Union(int a, int b)
        {
            parent[Find(a)] = Find(b);
        }

        for (int i = 0; i < cycles.Count; i++)
        {
            for (int j = i + 1; j < cycles.Count; j++)
            {
                if (CyclesBiconnected(cycles[i], cycles[j]))
                {
                    Union(i, j);
                }
            }
        }

        var groups = new Dictionary<int, List<List<IGraphEdge>>>();
        for (int i = 0; i < cycles.Count; i++)
        {
            int root = Find(i);
            if (!groups.TryGetValue(root, out var group))
            {
                group = new List<List<IGraphEdge>>();
                groups[root] = group;
            }

            group.Add(cycles[i]);
        }

        return groups.OrderBy(g => g.Key).Select(g => g.Value).ToList();
    }

    private static bool CyclesBiconnected(List<IGraphEdge> a, List<IGraphEdge> b)
    {
        if (a.Intersect(b).Any())
        {
            return true;
        }

        var nodesA = a.SelectMany(e => new[] { e.From.Id, e.To.Id }).ToHashSet();
        int sharedNodes = b.SelectMany(e => new[] { e.From.Id, e.To.Id }).Distinct().Count(nodesA.Contains);
        return sharedNodes >= 2;
    }
}
