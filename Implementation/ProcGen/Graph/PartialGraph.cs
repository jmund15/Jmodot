namespace Jmodot.Implementation.ProcGen.Graph;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     The mutable floor-graph builder the generator (P3a.6) drives to incrementally construct a
///     floor. HAS-A the shipped immutable kernel types (<see cref="GraphNode" /> / <see cref="GraphEdge" />)
///     — it is NOT itself an <see cref="IFloorGraph" />; <see cref="ToFloorGraph" /> produces one on demand.
///     Plain CLR (no Godot base), pure-engine: no rules, config, or geometry live here.
///     <para>
///         Determinism discipline: all membership / duplicate / open-port detection is by ordered linear
///         value-scan (never a reference-keyed <see cref="HashSet{T}" />), mirroring the shipped
///         GraphAnalyzer. Floor scale is &lt;100 nodes, so O(N)/O(E) scans are free.
///     </para>
/// </summary>
internal sealed class PartialGraph
{
    // ASCII unit separator U+001F — same delimiter discipline as GraphSignature/CandidateSlot, so a
    // SplitEdge-derived node id can never have its segment boundary forged by id content.
    private const char IdSep = (char)0x1F;

    private readonly List<GraphNode> _nodes = new();
    private readonly List<GraphEdge> _edges = new();

    private GraphNode? _source;
    private GraphNode? _sink;

    // Lazily-built, cached metrics snapshot. Null = stale/uncomputed; rebuilt on demand once a spine
    // exists. Nulled by Invalidate() from every topology mutation.
    private IGraphMetrics? _snapshot;

    // Test-injected metrics override. When non-null it wins over the real snapshot regardless of spine
    // state (lets rule tests drive PartialGraph pre-spine). Only ever set under #if TOOLS.
    private IGraphMetrics? _mockMetrics;

    public IReadOnlyList<GraphNode> Nodes => this._nodes;

    public IReadOnlyList<GraphEdge> Edges => this._edges;

    public int NodeCount => this._nodes.Count;

    public IGraphNode? Source => this._source;

    public IGraphNode? Sink => this._sink;

    public bool HasSpineEndpoints => this._source != null && this._sink != null;

    /// <summary>Caller allocates the id (determinism); throws on a duplicate id (id-keyed adjacency
    /// would silently collide downstream).</summary>
    internal GraphNode AddNode(StringName id, INodeTemplate template)
    {
        if (this.FindNodeById(id) != null)
        {
            throw new ArgumentException($"A node with id '{id}' already exists; node ids must be graph-unique.", nameof(id));
        }

        var node = new GraphNode(id, template);
        this._nodes.Add(node);
        this.Invalidate();
        return node;
    }

    /// <summary>Connects two member nodes, bridging each <see cref="IGraphPort" /> to its
    /// <see cref="IGraphPort.Name" /> (the kernel <see cref="GraphEdge" /> stores port-names). Rejects a
    /// re-bind of an already-bound port (ports are single-use) and any non-member endpoint.</summary>
    internal GraphEdge Connect(
        GraphNode from,
        IGraphPort fromPort,
        GraphNode to,
        IGraphPort toPort,
        bool gated = false,
        EdgeTraversal traversal = EdgeTraversal.Bidirectional,
        EdgeProvenance provenance = default)
    {
        this.RequireMember(from, nameof(from));
        this.RequireMember(to, nameof(to));

        if (!this.IsPortOpen(from.Id, fromPort.Name))
        {
            throw new ArgumentException(
                $"Port {from.Id}:{fromPort.Name} is already bound; ports are single-use.", nameof(fromPort));
        }

        if (!this.IsPortOpen(to.Id, toPort.Name))
        {
            throw new ArgumentException(
                $"Port {to.Id}:{toPort.Name} is already bound; ports are single-use.", nameof(toPort));
        }

        var edge = new GraphEdge(from, fromPort.Name, to, toPort.Name, gated, traversal, provenance);
        this._edges.Add(edge);
        this.Invalidate();
        return edge;
    }

    /// <summary>Splits a member edge by inserting a node mid-edge: removes the original, appends
    /// <c>from → [new]</c> then <c>[new] → to</c>. Outer faces keep the original edge's ports; inner
    /// faces use <paramref name="insert" />.Ports[0] (toward From) and Ports[1] (toward To) — throws if
    /// fewer than 2 ports. Gated/Traversal/Provenance are inherited by both halves.</summary>
    internal GraphNode SplitEdge(GraphEdge edge, INodeTemplate insert)
    {
        var idx = this.IndexOfEdge(edge);
        if (idx < 0)
        {
            throw new ArgumentException("Edge is not a member of this graph.", nameof(edge));
        }

        if (insert.Ports.Count < 2)
        {
            throw new ArgumentException(
                "Split-insert template must expose at least 2 ports (Ports[0] faces From, Ports[1] faces To).", nameof(insert));
        }

        var newId = new StringName(
            $"split{IdSep}{edge.From.Id}{IdSep}{edge.FromPort}{IdSep}{edge.To.Id}{IdSep}{edge.ToPort}");
        if (this.FindNodeById(newId) != null)
        {
            throw new ArgumentException($"Splitting this edge would produce a duplicate node id '{newId}'.", nameof(edge));
        }

        var inserted = new GraphNode(newId, insert);
        var firstHalf = new GraphEdge(edge.From, edge.FromPort, inserted, insert.Ports[0].Name, edge.IsGated, edge.Traversal, edge.Provenance);
        var secondHalf = new GraphEdge(inserted, insert.Ports[1].Name, edge.To, edge.ToPort, edge.IsGated, edge.Traversal, edge.Provenance);

        this._nodes.Add(inserted);
        this._edges.RemoveAt(idx);
        this._edges.Add(firstHalf);
        this._edges.Add(secondHalf);

        this.Invalidate();
        return inserted;
    }

    /// <summary>Commits the spine Source. Throws on re-commit (lifecycle) or a non-member node (a
    /// non-member endpoint yields -1 distances downstream).</summary>
    internal void SetSource(GraphNode node)
    {
        if (this._source != null)
        {
            throw new InvalidOperationException("Source has already been committed.");
        }

        this.RequireMember(node, nameof(node));
        this._source = node;
    }

    /// <summary>Commits the spine Sink. Throws on re-commit (lifecycle) or a non-member node.</summary>
    internal void SetSink(GraphNode node)
    {
        if (this._sink != null)
        {
            throw new InvalidOperationException("Sink has already been committed.");
        }

        this.RequireMember(node, nameof(node));
        this._sink = node;
    }

    /// <summary>Returns cached metrics over the current topology. <c>false</c> (uncached) until a spine
    /// exists, unless a test mock is injected (which wins regardless of spine). Builds lazily via
    /// <see cref="ToFloorGraph" /> and caches until the next topology mutation invalidates it.</summary>
    public bool TryGetMetrics(out IGraphMetrics metrics)
    {
        if (this._mockMetrics != null)
        {
            metrics = this._mockMetrics;
            return true;
        }

        if (!this.HasSpineEndpoints)
        {
            metrics = null!;
            return false;
        }

        this._snapshot ??= this.ToFloorGraph().Metrics;
        metrics = this._snapshot;
        return true;
    }

    /// <summary>Materializes an immutable <see cref="FloorGraph" /> from the current builder state. Throws
    /// until both endpoints are committed. Copies the internal lists defensively — <see cref="FloorGraph" />
    /// does not copy, so aliasing would let later builder mutation corrupt the produced graph.</summary>
    public FloorGraph ToFloorGraph()
    {
        if (!this.HasSpineEndpoints)
        {
            throw new InvalidOperationException(
                "Cannot build a FloorGraph before both Source and Sink are committed (HasSpineEndpoints is false).");
        }

        return new FloorGraph(
            new List<IGraphNode>(this._nodes),
            new List<IGraphEdge>(this._edges),
            this._source!,
            this._sink!);
    }

    /// <summary>Ordered placement candidates: for each node (in insertion order) each OPEN port (in
    /// template order) as a <see cref="PortSlot" />, then each edge (in insertion order) as an
    /// <see cref="EdgeSplitSlot" />. A port is open when no edge references <c>(node.Id, port.Name)</c> as
    /// an endpoint (single-use). Pure insertion-derived order — no hashing — so the sequence is
    /// deterministic.</summary>
    public IReadOnlyList<CandidateSlot> EnumerateSlots()
    {
        var slots = new List<CandidateSlot>();

        foreach (var node in this._nodes)
        {
            foreach (var port in node.Template.Ports)
            {
                if (this.IsPortOpen(node.Id, port.Name))
                {
                    slots.Add(new PortSlot(node, port));
                }
            }
        }

        foreach (var edge in this._edges)
        {
            slots.Add(new EdgeSplitSlot(edge));
        }

        return slots;
    }

    private bool IsPortOpen(StringName nodeId, StringName portName)
    {
        foreach (var e in this._edges)
        {
            if ((e.From.Id == nodeId && e.FromPort == portName) ||
                (e.To.Id == nodeId && e.ToPort == portName))
            {
                return false;
            }
        }

        return true;
    }

    private void Invalidate() => this._snapshot = null;

    #region Test Helpers
#if TOOLS
    /// <summary>Injects (or, with <c>null</c>, clears) a metrics mock that <see cref="TryGetMetrics" />
    /// returns regardless of spine state — lets rule tests drive PartialGraph before a spine exists.</summary>
    internal void SetMetricsForTest(IGraphMetrics? metrics) => this._mockMetrics = metrics;
#endif
    #endregion

    private GraphNode? FindNodeById(StringName id)
    {
        foreach (var n in this._nodes)
        {
            if (n.Id == id)
            {
                return n;
            }
        }

        return null;
    }

    private int IndexOfEdge(GraphEdge edge) => this._edges.IndexOf(edge);

    private void RequireMember(GraphNode node, string paramName)
    {
        if (!this._nodes.Contains(node))
        {
            throw new ArgumentException($"Node '{node.Id}' is not a member of this graph.", paramName);
        }
    }
}
