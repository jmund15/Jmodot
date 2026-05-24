namespace Jmodot.Implementation.ProcGen.Graph;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Computes the precomputed <see cref="IGraphMetrics" /> surface over a floor topology:
///     forward/reverse/gated-excluded BFS, terminal-aware degree scan, CHK directed dominators
///     for critical edges, and undirected Hopcroft-Tarjan for articulation nodes.
///     Self-contained and directly testable from hand-built topology.
///     <para>
///         Scale assumption: graphs are floor-bounded (~&lt;100 nodes), so eager full-metric
///         compute (3×BFS + CHK dominators + Hopcroft-Tarjan, each O(V+E)) at construction is
///         acceptable. Revisit lazy per-metric caching only if a profiler flags an outsized graph.
///     </para>
/// </summary>
public sealed class GraphAnalyzer
{
    private readonly IReadOnlyList<IGraphNode> _nodes;
    private readonly IReadOnlyList<IGraphEdge> _edges;
    private readonly IGraphNode _source;
    private readonly IGraphNode _sink;

    public GraphAnalyzer(
        IReadOnlyList<IGraphNode> nodes,
        IReadOnlyList<IGraphEdge> edges,
        IGraphNode source,
        IGraphNode sink)
    {
        this._nodes = nodes;
        this._edges = edges;
        this._source = source;
        this._sink = sink;
    }

    /// <summary>
    ///     Directed-outgoing adjacency keyed by node id: an edge contributes to its From-node's
    ///     list, and also to its To-node's list when bidirectional. Self-loops are filtered.
    ///     Backs <see cref="FloorGraph.EdgesFrom" />. (Compute builds its own id→id neighbor maps
    ///     internally; this edge-list view is the public directed-adjacency the graph exposes.)
    /// </summary>
    public static Dictionary<StringName, List<IGraphEdge>> BuildForwardAdjacency(
        IReadOnlyList<IGraphEdge> edges)
    {
        var adjacency = new Dictionary<StringName, List<IGraphEdge>>();
        foreach (var edge in edges)
        {
            if (edge.From.Id.Equals(edge.To.Id)) { continue; }
            AddEdge(adjacency, edge.From.Id, edge);
            if (edge.Traversal == EdgeTraversal.Bidirectional)
            {
                AddEdge(adjacency, edge.To.Id, edge);
            }
        }

        return adjacency;
    }

    private static void AddEdge(
        Dictionary<StringName, List<IGraphEdge>> adjacency,
        StringName key,
        IGraphEdge edge)
    {
        if (!adjacency.TryGetValue(key, out var list))
        {
            list = new List<IGraphEdge>();
            adjacency[key] = list;
        }

        list.Add(edge);
    }

    public IGraphMetrics Compute()
    {
        var outNeighbors = new Dictionary<StringName, List<StringName>>();
        var inNeighbors = new Dictionary<StringName, List<StringName>>();
        var degree = new Dictionary<StringName, int>();
        var undirected = new Dictionary<StringName, HashSet<StringName>>();

        foreach (var node in this._nodes)
        {
            degree.TryAdd(node.Id, 0);
        }

        foreach (var edge in this._edges)
        {
            var u = edge.From.Id;
            var v = edge.To.Id;
            if (u.Equals(v)) { continue; }

            AddNeighbor(outNeighbors, u, v);
            AddNeighbor(inNeighbors, v, u);
            if (edge.Traversal == EdgeTraversal.Bidirectional)
            {
                AddNeighbor(outNeighbors, v, u);
                AddNeighbor(inNeighbors, u, v);
            }

            degree[u] = degree.GetValueOrDefault(u) + 1;
            degree[v] = degree.GetValueOrDefault(v) + 1;
            AddUndirected(undirected, u, v);
            AddUndirected(undirected, v, u);
        }

        var distFromSource = Bfs(this._source.Id, outNeighbors);
        var distToSink = Bfs(this._sink.Id, inNeighbors);
        var ungatedReachable = ReachableExcluding(this._source.Id, null, ungatedOnly: true);
        var deadEnds = ComputeDeadEnds(degree);
        var criticalEdges = ComputeCriticalEdges(outNeighbors, inNeighbors, distFromSource);
        var articulation = ComputeArticulationNodes(undirected);

        return new ComputedMetrics(
            distFromSource,
            distToSink,
            ungatedReachable,
            deadEnds,
            criticalEdges,
            articulation);
    }

    private HashSet<StringName> ComputeDeadEnds(Dictionary<StringName, int> degree)
    {
        var deadEnds = new HashSet<StringName>();
        foreach (var node in this._nodes)
        {
            if (node.Id.Equals(this._source.Id) || node.Id.Equals(this._sink.Id)) { continue; }
            if (degree.GetValueOrDefault(node.Id) == 1) { deadEnds.Add(node.Id); }
        }

        return deadEnds;
    }

    private List<IGraphEdge> ComputeCriticalEdges(
        Dictionary<StringName, List<StringName>> outNeighbors,
        Dictionary<StringName, List<StringName>> inNeighbors,
        Dictionary<StringName, int> distFromSource)
    {
        var result = new List<IGraphEdge>();

        // Baseline: if Sink is already unreachable, every cut is trivial — return empty.
        if (!distFromSource.ContainsKey(this._sink.Id)) { return result; }

        var idom = ComputeDominators(outNeighbors, inNeighbors);
        if (!idom.ContainsKey(this._sink.Id)) { return result; }

        // Walk Sink's dominator chain to Source; candidate edges cross idom[v] → v on it.
        var cur = this._sink.Id;
        while (!cur.Equals(this._source.Id))
        {
            if (!idom.TryGetValue(cur, out var u)) { break; }
            var v = cur;
            foreach (var edge in this._edges)
            {
                if (edge.From.Id.Equals(edge.To.Id)) { continue; }
                var provides =
                    (edge.From.Id.Equals(u) && edge.To.Id.Equals(v)) ||
                    (edge.Traversal == EdgeTraversal.Bidirectional &&
                     edge.From.Id.Equals(v) && edge.To.Id.Equals(u));
                if (!provides) { continue; }

                // Confirm uniqueness: removing this single edge must strand v from Source.
                var reachable = ReachableExcluding(this._source.Id, edge, ungatedOnly: false);
                if (!reachable.Contains(v) && !ContainsByReference(result, edge))
                {
                    result.Add(edge);
                }
            }

            cur = u;
        }

        return result;
    }

    /// <summary>Cooper-Harvey-Kennedy iterative dominator tree rooted at Source.</summary>
    private Dictionary<StringName, StringName> ComputeDominators(
        Dictionary<StringName, List<StringName>> outNeighbors,
        Dictionary<StringName, List<StringName>> inNeighbors)
    {
        var postorder = new Dictionary<StringName, int>();
        var order = new List<StringName>();
        var visited = new HashSet<StringName>();
        DfsPostorder(this._source.Id, outNeighbors, visited, postorder, order);

        var idom = new Dictionary<StringName, StringName> { [this._source.Id] = this._source.Id };
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = order.Count - 1; i >= 0; i--)
            {
                var b = order[i];
                if (b.Equals(this._source.Id)) { continue; }

                StringName? newIdom = null;
                if (inNeighbors.TryGetValue(b, out var preds))
                {
                    foreach (var p in preds)
                    {
                        if (!postorder.ContainsKey(p) || !idom.ContainsKey(p)) { continue; }
                        newIdom = newIdom == null ? p : Intersect(p, newIdom, idom, postorder);
                    }
                }

                if (newIdom != null && (!idom.TryGetValue(b, out var existing) || !existing.Equals(newIdom)))
                {
                    idom[b] = newIdom;
                    changed = true;
                }
            }
        }

        return idom;
    }

    private static StringName Intersect(
        StringName a,
        StringName b,
        Dictionary<StringName, StringName> idom,
        Dictionary<StringName, int> postorder)
    {
        while (!a.Equals(b))
        {
            while (postorder[a] < postorder[b]) { a = idom[a]; }
            while (postorder[b] < postorder[a]) { b = idom[b]; }
        }

        return a;
    }

    private static void DfsPostorder(
        StringName start,
        Dictionary<StringName, List<StringName>> outNeighbors,
        HashSet<StringName> visited,
        Dictionary<StringName, int> postorder,
        List<StringName> order)
    {
        // Iterative postorder. (ArticulationDfs below recurses instead — both are bounded by the
        // floor-scale node assumption; the iterative form here is just the dominator path's choice.)
        var stack = new Stack<(StringName node, int next)>();
        stack.Push((start, 0));
        visited.Add(start);
        while (stack.Count > 0)
        {
            var (node, next) = stack.Pop();
            var neighbors = outNeighbors.TryGetValue(node, out var ns) ? ns : null;
            if (neighbors != null && next < neighbors.Count)
            {
                stack.Push((node, next + 1));
                var child = neighbors[next];
                if (visited.Add(child)) { stack.Push((child, 0)); }
            }
            else
            {
                postorder[node] = order.Count;
                order.Add(node);
            }
        }
    }

    private List<IGraphNode> ComputeArticulationNodes(
        Dictionary<StringName, HashSet<StringName>> undirected)
    {
        var disc = new Dictionary<StringName, int>();
        var low = new Dictionary<StringName, int>();
        var articulationIds = new HashSet<StringName>();
        var timer = 0;

        foreach (var node in this._nodes)
        {
            if (!disc.ContainsKey(node.Id))
            {
                ArticulationDfs(node.Id, default, true, undirected, disc, low, articulationIds, ref timer);
            }
        }

        var result = new List<IGraphNode>();
        foreach (var node in this._nodes)
        {
            if (articulationIds.Contains(node.Id)) { result.Add(node); }
        }

        return result;
    }

    private static void ArticulationDfs(
        StringName u,
        StringName? parent,
        bool isRoot,
        Dictionary<StringName, HashSet<StringName>> undirected,
        Dictionary<StringName, int> disc,
        Dictionary<StringName, int> low,
        HashSet<StringName> articulationIds,
        ref int timer)
    {
        disc[u] = low[u] = timer++;
        var children = 0;
        if (undirected.TryGetValue(u, out var neighbors))
        {
            foreach (var v in neighbors)
            {
                if (!disc.ContainsKey(v))
                {
                    children++;
                    ArticulationDfs(v, u, false, undirected, disc, low, articulationIds, ref timer);
                    if (low[v] < low[u]) { low[u] = low[v]; }
                    if (!isRoot && low[v] >= disc[u]) { articulationIds.Add(u); }
                }
                else if (parent == null || !v.Equals(parent))
                {
                    if (disc[v] < low[u]) { low[u] = disc[v]; }
                }
            }
        }

        if (isRoot && children > 1) { articulationIds.Add(u); }
    }

    private Dictionary<StringName, int> Bfs(
        StringName start,
        Dictionary<StringName, List<StringName>> adjacency)
    {
        var dist = new Dictionary<StringName, int> { [start] = 0 };
        var queue = new Queue<StringName>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!adjacency.TryGetValue(node, out var neighbors)) { continue; }
            foreach (var next in neighbors)
            {
                if (dist.ContainsKey(next)) { continue; }
                dist[next] = dist[node] + 1;
                queue.Enqueue(next);
            }
        }

        return dist;
    }

    private HashSet<StringName> ReachableExcluding(StringName start, IGraphEdge? exclude, bool ungatedOnly)
    {
        var visited = new HashSet<StringName> { start };
        var queue = new Queue<StringName>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var edge in this._edges)
            {
                if (exclude != null && ReferenceEquals(edge, exclude)) { continue; }
                if (edge.From.Id.Equals(edge.To.Id)) { continue; }
                if (ungatedOnly && edge.IsGated) { continue; }

                StringName? next = null;
                if (edge.From.Id.Equals(node)) { next = edge.To.Id; }
                else if (edge.Traversal == EdgeTraversal.Bidirectional && edge.To.Id.Equals(node)) { next = edge.From.Id; }

                if (next != null && visited.Add(next)) { queue.Enqueue(next); }
            }
        }

        return visited;
    }

    private static void AddNeighbor(Dictionary<StringName, List<StringName>> map, StringName key, StringName value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<StringName>();
            map[key] = list;
        }

        list.Add(value);
    }

    private static void AddUndirected(Dictionary<StringName, HashSet<StringName>> map, StringName key, StringName value)
    {
        if (!map.TryGetValue(key, out var set))
        {
            set = new HashSet<StringName>();
            map[key] = set;
        }

        set.Add(value);
    }

    private static bool ContainsByReference(List<IGraphEdge> list, IGraphEdge edge)
    {
        foreach (var existing in list)
        {
            if (ReferenceEquals(existing, edge)) { return true; }
        }

        return false;
    }

    private sealed class ComputedMetrics : IGraphMetrics
    {
        private readonly Dictionary<StringName, int> _distFromSource;
        private readonly Dictionary<StringName, int> _distToSink;
        private readonly HashSet<StringName> _ungatedReachable;
        private readonly HashSet<StringName> _deadEnds;

        public ComputedMetrics(
            Dictionary<StringName, int> distFromSource,
            Dictionary<StringName, int> distToSink,
            HashSet<StringName> ungatedReachable,
            HashSet<StringName> deadEnds,
            List<IGraphEdge> criticalEdges,
            List<IGraphNode> articulationNodes)
        {
            this._distFromSource = distFromSource;
            this._distToSink = distToSink;
            this._ungatedReachable = ungatedReachable;
            this._deadEnds = deadEnds;
            this.CriticalEdges = criticalEdges;
            this.ArticulationNodes = articulationNodes;
        }

        public bool IsDeadEnd(IGraphNode node) => this._deadEnds.Contains(node.Id);

        public int DistanceFromSource(IGraphNode node) =>
            this._distFromSource.TryGetValue(node.Id, out var d) ? d : -1;

        public int DistanceToSink(IGraphNode node) =>
            this._distToSink.TryGetValue(node.Id, out var d) ? d : -1;

        public bool IsReachableFromSourceWithoutGates(IGraphNode node) =>
            this._ungatedReachable.Contains(node.Id);

        public IReadOnlyList<IGraphEdge> CriticalEdges { get; }

        public IReadOnlyList<IGraphNode> ArticulationNodes { get; }
    }
}
