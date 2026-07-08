namespace Jmodot.Implementation.ProcGen.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Jmodot.Core.ProcGen;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.ProcGen.Spatial;
using Jmodot.Implementation.ProcGen.Graph;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
///     RNG-free deterministic grid embedder (design-se §4). Embedding unit is the biconnected
///     block: blocks embed holistically first (most-constrained-first, with port re-binding and
///     conflict-directed backjump under the repair budget), the tree remainder embeds greedily
///     after with its stage-1 bindings kept. Pose enumeration order is pinned and part of the
///     determinism contract: faces in template-port order, ascending slide offsets, Yaw0/90/180/270,
///     insertion-ordered occupancy, FNV content-derived tie-breaks. Coordinates are search-relative
///     until emission, where the layout normalizes to the envelope's min corner — envelope
///     violations are therefore SIZE checks during search (translation-invariant).
/// </summary>
public sealed class GridFloorEmbedder : IFloorEmbedder
{
    private const int AxisSumStateCap = 4096;

    public FloorEmbedResult Embed(IFloorGraph topology, GeometryEnvelope envelope, EmbedderSettings settings)
    {
        ValidateEmbedArgs(topology, envelope, settings);
        var infos = BuildNodeInfos(topology);
        var state = NewState(topology, envelope, settings);
        FloorEmbedResult? failure = PlaceAll(topology, infos, state);
        return Capture(failure ?? EmitResult(topology, infos, state), state);
    }

    /// <summary>
    ///     Opens a progressive embedding session: the returned <see cref="ILayoutAdvisor" /> embeds +
    ///     freezes <paramref name="backbone" /> immediately, then answers geometric queries and
    ///     trial-commits decorations for the generator. See <see cref="ILayoutAdvisor" />.
    /// </summary>
    public ILayoutAdvisor BeginSession(IFloorGraph backbone, GeometryEnvelope envelope, EmbedderSettings settings)
        => new LayoutSession(this, backbone, envelope, settings);

    /// <summary>
    ///     Frozen-spine progressive seam (Design A): embeds a backbone sub-graph alone and returns its
    ///     populated <see cref="SearchState" /> — every backbone node carries a fixed pose + occupancy.
    ///     Pair with <see cref="Extend" />, which resumes from this state to place loop/branch
    ///     decorations WITHOUT moving the frozen backbone (the placement order skips nodes already in
    ///     <c>state.Poses</c>). Lets the generator commit only geometry it has validated against the
    ///     real grid instead of re-rolling the whole topology on an embed miss.
    /// </summary>
    internal SearchState BuildState(IFloorGraph backbone, GeometryEnvelope envelope, EmbedderSettings settings)
    {
        ValidateEmbedArgs(backbone, envelope, settings);
        var infos = BuildNodeInfos(backbone);
        var state = NewState(backbone, envelope, settings);
        PlaceAll(backbone, infos, state);
        return state;
    }

    /// <summary>
    ///     Resumes a frozen <paramref name="state" /> (from <see cref="BuildState" />) to embed the
    ///     FULL graph: backbone poses are reused unchanged (skipped by the placement order), only the
    ///     decoration nodes are searched, and the budget is re-sized to the full graph. Returns the
    ///     complete layout, or the typed failure if a decoration cannot be placed on the frozen frame.
    ///     <paramref name="fullGraph" /> MUST share node/edge INSTANCES with the backbone handed to
    ///     <see cref="BuildState" /> — edge bindings are identity-keyed, so a freshly-rebuilt graph
    ///     with equal ids but different instances loses the frozen bindings. The generator satisfies
    ///     this by construction (both are ToFloorGraph snapshots of the same growing PartialGraph).
    /// </summary>
    internal FloorEmbedResult Extend(SearchState state, IFloorGraph fullGraph, GeometryEnvelope envelope, EmbedderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateEmbedArgs(fullGraph, envelope, settings);
        var infos = BuildNodeInfos(fullGraph);
        state.Budget = ResolveEffectiveBudget(fullGraph, settings);
        state.FrozenNodes.Clear();
        foreach (StringName placed in state.Poses.Keys)
        {
            state.FrozenNodes.Add(placed);
        }

        FloorEmbedResult? failure = PlaceAll(fullGraph, infos, state);
        return Capture(failure ?? EmitResult(fullGraph, infos, state), state);
    }

    private static void ValidateEmbedArgs(IFloorGraph topology, GeometryEnvelope envelope, EmbedderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.RepairBudget <= 0)
        {
            throw new ResourceConfigurationException(
                $"EmbedderSettings.RepairBudget must be positive; got {settings.RepairBudget}.", settings);
        }

        envelope.Validate();
    }

    private static SearchState NewState(IFloorGraph topology, GeometryEnvelope envelope, EmbedderSettings settings)
        => new SearchState(ResolveEffectiveBudget(topology, settings), envelope.SizeCells);

    private static int ResolveEffectiveBudget(IFloorGraph topology, EmbedderSettings settings)
    {
        int loopEdges = 0;
        foreach (IGraphEdge edge in topology.Edges)
        {
            if (edge.Provenance.Kind == EdgeProvenanceKind.Loop)
            {
                loopEdges++;
            }
        }

        return ResolveBudget(
            settings.DynamicBudget, settings.RepairBudget, settings.BudgetBase, settings.BudgetPerNode,
            settings.BudgetPerLoopEdge, settings.MaxBudget, topology.Nodes.Count, loopEdges);
    }

    // Block pass + remainder over whatever is NOT already placed in `state` — the one shared placement
    // core behind Embed (fresh state) and Extend (state pre-seeded with frozen backbone poses).
    private static FloorEmbedResult? PlaceAll(IFloorGraph topology, Dictionary<StringName, NodeInfo> infos, SearchState state)
    {
        var decomposition = BlockExtractor.Extract(topology);
        foreach (IGraphEdge treeEdge in decomposition.TreeEdges)
        {
            state.ReservedPorts.Add((treeEdge.From.Id, treeEdge.FromPort));
            state.ReservedPorts.Add((treeEdge.To.Id, treeEdge.ToPort));
        }

        foreach (GraphBlock block in OrderBlocks(decomposition.Blocks))
        {
            FloorEmbedResult? parityFailure = CheckClosureParity(block, infos);
            if (parityFailure != null)
            {
                return parityFailure;
            }

            FloorEmbedResult? blockFailure = EmbedBlock(block, infos, state);
            if (blockFailure != null)
            {
                return blockFailure;
            }
        }

        return EmbedRemainder(topology, infos, state);
    }

#region Test Helpers
#if TOOLS
    internal IReadOnlyList<StringName>? LastPlacementTrace { get; private set; }
#endif
#endregion

    private FloorEmbedResult Capture(FloorEmbedResult result, SearchState state)
    {
#if TOOLS
        this.LastPlacementTrace = state.PlacementOrder.ToList();
#endif
        return result;
    }

    // ---- setup ----

    private sealed class NodeInfo
    {
        internal NodeInfo(IGraphNode node, ISpatialNodeTemplate template, IReadOnlyList<ISpatialPort> ports, List<IGraphEdge> edges)
        {
            this.Node = node;
            this.Template = template;
            this.Ports = ports;
            this.Edges = edges;
        }

        internal IGraphNode Node { get; }

        internal ISpatialNodeTemplate Template { get; }

        internal IReadOnlyList<ISpatialPort> Ports { get; }

        internal List<IGraphEdge> Edges { get; }

        internal Vector3I Footprint => this.Template.FootprintCells;
    }

    private static Dictionary<StringName, NodeInfo> BuildNodeInfos(IFloorGraph topology)
    {
        var infos = new Dictionary<StringName, NodeInfo>();
        foreach (IGraphNode node in topology.Nodes)
        {
            if (node.Template is not ISpatialNodeTemplate spatial)
            {
                throw new ArgumentException(
                    $"Node '{node.Id}' template '{node.Template.TemplateId}' does not implement ISpatialNodeTemplate.", nameof(topology));
            }

            Vector3I footprint = spatial.FootprintCells;
            if (footprint.X <= 0 || footprint.Y <= 0 || footprint.Z <= 0)
            {
                throw new ArgumentException(
                    $"Node '{node.Id}' template '{node.Template.TemplateId}' has an unbaked footprint {footprint}.", nameof(topology));
            }

            var ports = new List<ISpatialPort>(node.Template.Ports.Count);
            foreach (IGraphPort port in node.Template.Ports)
            {
                if (port is not ISpatialPort spatialPort)
                {
                    throw new ArgumentException(
                        $"Node '{node.Id}' port '{port.Name}' does not implement ISpatialPort.", nameof(topology));
                }

                ports.Add(spatialPort);
            }

            infos[node.Id] = new NodeInfo(node, spatial, ports, new List<IGraphEdge>());
        }

        foreach (IGraphEdge edge in topology.Edges)
        {
            infos[edge.From.Id].Edges.Add(edge);
            infos[edge.To.Id].Edges.Add(edge);
        }

        return infos;
    }

    private static IEnumerable<GraphBlock> OrderBlocks(IReadOnlyList<GraphBlock> blocks)
    {
        return blocks
            .OrderByDescending(b => b.Edges.Count)
            .ThenBy(b => Fnv1A64(string.Join(string.Empty, b.Nodes.Select(n => n.Id.ToString()).OrderBy(s => s, StringComparer.Ordinal))));
    }

    // ---- ClosureParity pre-search check ----

    private static FloorEmbedResult? CheckClosureParity(GraphBlock block, Dictionary<StringName, NodeInfo> infos)
    {
        foreach (IReadOnlyList<IGraphEdge> cycle in block.Cycles)
        {
            var cycleNodes = CycleNodeRing(cycle);
            var perNodeDeltas = cycleNodes
                .Select(id => AnchorDeltaOptions(infos[id]))
                .ToList();

            bool parityFeasible = JointParityFeasible(perNodeDeltas);
            bool magnitudeFeasible = AxisSumFeasible(perNodeDeltas, d => d.X) && AxisSumFeasible(perNodeDeltas, d => d.Y);
            if (parityFeasible && magnitudeFeasible)
            {
                continue;
            }

            StringName failing = cycleNodes
                .OrderBy(id => Fnv1A64(id.ToString()))
                .ThenBy(id => id.ToString(), StringComparer.Ordinal)
                .First();
            return FloorEmbedResult.Failure(EmbedFailureCause.ClosureParity, failing);
        }

        return null;
    }

    // internal (not private) so the ring walk is directly unit-testable without a full Embed —
    // it is the one place a malformed (non-simple) Loop cycle would hang the pipeline.
    internal static List<StringName> CycleNodeRing(IReadOnlyList<IGraphEdge> cycle)
    {
        var adjacency = new Dictionary<StringName, List<StringName>>();
        foreach (IGraphEdge edge in cycle)
        {
            adjacency.TryAdd(edge.From.Id, new List<StringName>());
            adjacency.TryAdd(edge.To.Id, new List<StringName>());
            adjacency[edge.From.Id].Add(edge.To.Id);
            adjacency[edge.To.Id].Add(edge.From.Id);
        }

        var ring = new List<StringName>();
        StringName start = cycle[0].From.Id;
        StringName previous = start;
        StringName current = start;
        do
        {
            ring.Add(current);

            // A simple ring visits each node once, so the walk closes within adjacency.Count steps.
            // A non-simple cycle (a node of degree > 2 — e.g. a Loop route sharing an interior node
            // with its tree path) would otherwise truncate early or never re-reach start (infinite
            // hang). Fail loud with the offending node count instead of wedging the embed.
            if (ring.Count > adjacency.Count)
            {
                throw new InvalidOperationException(
                    $"CycleNodeRing walked {ring.Count} nodes over a {adjacency.Count}-node cycle: " +
                    "the source graph produced a non-simple Loop cycle (a node with degree > 2). " +
                    "This indicates a malformed topology upstream of the embedder.");
            }

            List<StringName> neighbors = adjacency[current];
            StringName next = neighbors[0] == previous && neighbors.Count > 1 ? neighbors[1] : neighbors[0];
            previous = current;
            current = next;
        }
        while (current != start);

        // The walk closed back onto start; a simple ring will have visited every node exactly once.
        // A shorter ring means it short-circuited across a shared node (figure-eight), skipping a
        // branch — the truncated-ring failure mode. Reject it rather than embed a partial cycle.
        if (ring.Count != adjacency.Count)
        {
            throw new InvalidOperationException(
                $"CycleNodeRing closed after {ring.Count} of {adjacency.Count} nodes: the source graph " +
                "produced a non-simple Loop cycle (a branch left unvisited), indicating malformed topology upstream.");
        }

        return ring;
    }

    /// <summary>
    ///     Every achievable (Δx, Δz) between two distinct port anchors of the node, over all four
    ///     yaws — the node's possible contribution to a cycle's closure displacement.
    /// </summary>
    private static List<Vector2I> AnchorDeltaOptions(NodeInfo info)
    {
        var options = new List<Vector2I>();
        foreach (ISpatialPort exit in info.Ports)
        {
            foreach (ISpatialPort entry in info.Ports)
            {
                if (ReferenceEquals(exit, entry))
                {
                    continue;
                }

                for (int yaw = 0; yaw < 4; yaw++)
                {
                    Vector3I a = SpatialPoseMath.RotateLocalAnchor(exit, info.Footprint, (YawQuadrant)yaw);
                    Vector3I b = SpatialPoseMath.RotateLocalAnchor(entry, info.Footprint, (YawQuadrant)yaw);
                    options.Add(new Vector2I(a.X - b.X, a.Z - b.Z));
                }
            }
        }

        return options;
    }

    private static bool JointParityFeasible(List<List<Vector2I>> perNodeDeltas)
    {
        var reachable = new HashSet<(int X, int Z)> { (0, 0) };
        foreach (List<Vector2I> options in perNodeDeltas)
        {
            var next = new HashSet<(int X, int Z)>();
            foreach ((int X, int Z) sum in reachable)
            {
                foreach (Vector2I delta in options)
                {
                    next.Add(((sum.X + delta.X) & 1, (sum.Z + delta.Y) & 1));
                }
            }

            reachable = next;
        }

        return reachable.Contains((0, 0));
    }

    private static bool AxisSumFeasible(List<List<Vector2I>> perNodeDeltas, Func<Vector2I, int> axis)
    {
        var reachable = new HashSet<int> { 0 };
        foreach (List<Vector2I> options in perNodeDeltas)
        {
            var next = new HashSet<int>();
            foreach (int sum in reachable)
            {
                foreach (Vector2I delta in options)
                {
                    next.Add(sum + axis(delta));
                }
            }

            if (next.Count > AxisSumStateCap)
            {
                return true;
            }

            reachable = next;
        }

        return reachable.Contains(0);
    }

    // ---- search state ----

    internal sealed class SearchState
    {
        internal SearchState(int budget, Vector3I envelopeSize)
        {
            this.Budget = budget;
            this.EnvelopeSize = envelopeSize;
        }

        internal Dictionary<StringName, CellPose> Poses { get; } = new();

        internal OccupancyIndex Occupancy { get; private set; } = new();

        internal HashSet<(StringName NodeId, StringName PortName)> BoundPorts { get; } = new();

        internal Dictionary<IGraphEdge, (StringName FromPort, StringName ToPort)> EdgeBindings { get; } = new();

        internal List<(StringName Id, Vector3I Origin, Vector3I Size)> Regions { get; } = new();

        /// <summary>Stage-1 ports of tree edges — off-limits to block re-binding, since chains/leaves keep their stage-1 bindings.</summary>
        internal HashSet<(StringName NodeId, StringName PortName)> ReservedPorts { get; } = new();

        internal List<StringName> PlacementOrder { get; } = new();

        /// <summary>
        ///     Nodes already placed when the CURRENT placement pass began (populated by <see cref="Extend" />).
        ///     Their declared edge ports are HONORED during rebind, so a committed loop/branch cannot
        ///     cannibalize a spare port that a later sub-graph needs. Empty for the holistic
        ///     <see cref="Embed" /> and for <see cref="BuildState" /> (full rebind freedom — unchanged behavior).
        /// </summary>
        internal HashSet<StringName> FrozenNodes { get; } = new();

        internal int Budget { get; set; }

        internal Vector3I EnvelopeSize { get; }

        internal EmbedFailureCause LastConflictCause { get; set; } = EmbedFailureCause.NoBinding;

        /// <summary>
        ///     Deep copy for trial placement: a candidate loop/branch is embedded onto the clone, which is
        ///     ADOPTED only if it succeeds — so a failed trial leaves the committed state pristine (no
        ///     residual poses, occupancy, bindings, or reserved ports to corrupt later placements).
        /// </summary>
        internal SearchState Clone()
        {
            var copy = new SearchState(this.Budget, this.EnvelopeSize)
            {
                Occupancy = this.Occupancy.Clone(),
                LastConflictCause = this.LastConflictCause,
            };
            foreach (var pose in this.Poses) { copy.Poses[pose.Key] = pose.Value; }
            foreach (var bound in this.BoundPorts) { copy.BoundPorts.Add(bound); }
            foreach (var binding in this.EdgeBindings) { copy.EdgeBindings[binding.Key] = binding.Value; }
            foreach (var reserved in this.ReservedPorts) { copy.ReservedPorts.Add(reserved); }
            foreach (var frozen in this.FrozenNodes) { copy.FrozenNodes.Add(frozen); }
            copy.Regions.AddRange(this.Regions);
            copy.PlacementOrder.AddRange(this.PlacementOrder);
            return copy;
        }
    }

    private sealed class Candidate
    {
        internal Candidate(CellPose pose, List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort)> bindings)
        {
            this.Pose = pose;
            this.Bindings = bindings;
        }

        internal CellPose Pose { get; }

        internal List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort)> Bindings { get; }
    }

    private sealed class Frame
    {
        internal Frame(StringName nodeId, List<Candidate> candidates)
        {
            this.NodeId = nodeId;
            this.Candidates = candidates;
        }

        internal StringName NodeId { get; }

        internal List<Candidate> Candidates { get; }

        internal int Cursor { get; set; }

        internal Candidate? Applied { get; set; }
    }

    // ---- block pass ----

    private static FloorEmbedResult? EmbedBlock(GraphBlock block, Dictionary<StringName, NodeInfo> infos, SearchState state)
    {
        var order = BlockPlacementOrder(block, state);
        return RunSearch(order, rebind: true, infos, state);
    }

    /// <summary>
    ///     Cycle-grouped placement order: walk each constituent cycle as a ring, rotated to begin
    ///     at an already-ordered node, so every route's CLOSING node meets its closure conflict
    ///     immediately after the route's own decisions — which keeps the backjump local to the
    ///     branch that caused the conflict instead of churning unrelated routes.
    /// </summary>
    private static List<StringName> BlockPlacementOrder(GraphBlock block, SearchState state)
    {
        StringName start = block.Nodes
            .Select(n => n.Id)
            .FirstOrDefault(id => state.Poses.ContainsKey(id))
            ?? block.Nodes
                .OrderByDescending(n => block.Edges.Count(e => e.From.Id == n.Id || e.To.Id == n.Id))
                .ThenBy(n => Fnv1A64(n.Id.ToString()))
                .ThenBy(n => n.Id.ToString(), StringComparer.Ordinal)
                .First()
                .Id;

        var order = new List<StringName>();
        var seen = new HashSet<StringName>();

        void Append(StringName id)
        {
            if (seen.Add(id))
            {
                order.Add(id);
            }
        }

        Append(start);
        foreach (IReadOnlyList<IGraphEdge> cycle in block.Cycles)
        {
            List<StringName> ring = CycleNodeRing(cycle);
            int anchorIndex = ring.FindIndex(seen.Contains);
            if (anchorIndex < 0)
            {
                anchorIndex = 0;
            }

            for (int i = 0; i < ring.Count; i++)
            {
                Append(ring[(anchorIndex + i) % ring.Count]);
            }
        }

        foreach (IGraphNode node in block.Nodes)
        {
            Append(node.Id);
        }

        return order.Where(id => !state.Poses.ContainsKey(id)).ToList();
    }

    // ---- remainder pass ----

    private static FloorEmbedResult? EmbedRemainder(IFloorGraph topology, Dictionary<StringName, NodeInfo> infos, SearchState state)
    {
        while (state.Poses.Count < topology.Nodes.Count)
        {
            IGraphEdge? frontier = topology.Edges.FirstOrDefault(e =>
                state.Poses.ContainsKey(e.From.Id) != state.Poses.ContainsKey(e.To.Id));

            if (frontier != null)
            {
                StringName next = state.Poses.ContainsKey(frontier.From.Id) ? frontier.To.Id : frontier.From.Id;
                FloorEmbedResult? failure = RunSearch(new List<StringName> { next }, rebind: false, infos, state);
                if (failure != null)
                {
                    return failure;
                }

                continue;
            }

            StringName seed = topology.Source != null && !state.Poses.ContainsKey(topology.Source.Id)
                ? topology.Source.Id
                : topology.Nodes.First(n => !state.Poses.ContainsKey(n.Id)).Id;
            FloorEmbedResult? seedFailure = RunSearch(new List<StringName> { seed }, rebind: false, infos, state);
            if (seedFailure != null)
            {
                return seedFailure;
            }
        }

        return null;
    }

    // ---- the deterministic DFS ----

    private static FloorEmbedResult? RunSearch(
        List<StringName> order,
        bool rebind,
        Dictionary<StringName, NodeInfo> infos,
        SearchState state)
    {
        var frames = new List<Frame>();
        int depth = 0;
        while (depth < order.Count)
        {
            if (frames.Count == depth)
            {
                frames.Add(new Frame(order[depth], GenerateCandidates(order[depth], rebind, infos, state)));
            }

            Frame frame = frames[depth];
            bool advanced = false;
            while (frame.Cursor < frame.Candidates.Count)
            {
                Candidate candidate = frame.Candidates[frame.Cursor];
                frame.Cursor++;
                Apply(frame.NodeId, candidate, infos, state);
                frame.Applied = candidate;
                advanced = true;
                break;
            }

            if (advanced)
            {
                depth++;
                continue;
            }

            frames.RemoveAt(depth);
            if (depth == 0)
            {
                return FloorEmbedResult.Failure(state.LastConflictCause, frame.NodeId);
            }

            // Conflict-directed backjump: return to the failing node's own most recent constraint
            // (the deepest placed NEIGHBOR frame), never chronologically through unrelated
            // branches — intermediate frames are unwound and will re-place after the repair.
            int culprit = CulpritDepth(order, depth, frame.NodeId, infos);
            for (int k = depth - 1; k >= culprit; k--)
            {
                Frame unwound = frames[k];
                Undo(unwound.NodeId, unwound.Applied!, infos, state);
                unwound.Applied = null;
                if (k > culprit)
                {
                    frames.RemoveAt(k);
                }
            }

            depth = culprit;
            state.Budget--;
            if (state.Budget <= 0)
            {
                return FloorEmbedResult.Failure(state.LastConflictCause, frame.NodeId);
            }
        }

        return null;
    }

    private static int CulpritDepth(List<StringName> order, int failedDepth, StringName failedNode, Dictionary<StringName, NodeInfo> infos)
    {
        var neighbors = infos[failedNode].Edges
            .Select(e => OtherEnd(e, failedNode))
            .ToHashSet();
        for (int j = failedDepth - 1; j >= 0; j--)
        {
            if (neighbors.Contains(order[j]))
            {
                return j;
            }
        }

        return failedDepth - 1;
    }

    private static List<Candidate> GenerateCandidates(
        StringName nodeId,
        bool rebind,
        Dictionary<StringName, NodeInfo> infos,
        SearchState state)
    {
        NodeInfo info = infos[nodeId];
        int spaceConflicts = 0;
        int bindingConflicts = 0;
        var candidates = new List<Candidate>();

        var placedNeighborEdges = info.Edges
            .Where(e => state.Poses.ContainsKey(OtherEnd(e, nodeId)))
            .ToList();

        if (placedNeighborEdges.Count == 0)
        {
            foreach (CellPose pose in RootPoses(info, state))
            {
                if (PassesPlacementFilters(nodeId, pose, new List<(IGraphEdge, StringName, StringName)>(), info, state, ref spaceConflicts, ref bindingConflicts))
                {
                    candidates.Add(new Candidate(pose, new List<(IGraphEdge, StringName, StringName)>()));
                }
            }
        }
        else
        {
            IGraphEdge anchorEdge = placedNeighborEdges[0];
            foreach (Candidate candidate in AnchoredCandidates(nodeId, anchorEdge, placedNeighborEdges, rebind, info, infos, state, ref spaceConflicts, ref bindingConflicts))
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            state.LastConflictCause = spaceConflicts >= bindingConflicts && spaceConflicts > 0
                ? EmbedFailureCause.SpaceTight
                : EmbedFailureCause.NoBinding;
        }

        return candidates;
    }

    private static IEnumerable<CellPose> RootPoses(NodeInfo info, SearchState state)
    {
        if (state.Poses.Count == 0)
        {
            for (int yaw = 0; yaw < 4; yaw++)
            {
                yield return new CellPose(Vector3I.Zero, (YawQuadrant)yaw);
            }

            yield break;
        }

        (Vector3I min, Vector3I max) = CurrentBounds(state, infosRegionHint: null);
        Vector3I span = SpatialPoseMath.RotateSize(info.Footprint, YawQuadrant.Yaw0);
        for (int z = min.Z - span.Z; z <= max.Z + 1; z++)
        {
            for (int x = min.X - span.X; x <= max.X + 1; x++)
            {
                for (int yaw = 0; yaw < 4; yaw++)
                {
                    yield return new CellPose(new Vector3I(x, 0, z), (YawQuadrant)yaw);
                }
            }
        }
    }

    private static IEnumerable<Candidate> AnchoredCandidates(
        StringName nodeId,
        IGraphEdge anchorEdge,
        List<IGraphEdge> placedNeighborEdges,
        bool rebind,
        NodeInfo info,
        Dictionary<StringName, NodeInfo> infos,
        SearchState state,
        ref int spaceConflicts,
        ref int bindingConflicts)
    {
        var results = new List<Candidate>();
        StringName anchorId = OtherEnd(anchorEdge, nodeId);
        NodeInfo anchorInfo = infos[anchorId];
        CellPose anchorPose = state.Poses[anchorId];

        foreach ((ISpatialPort anchorPort, ISpatialPort myPort) in PortPairs(anchorEdge, anchorId, nodeId, anchorInfo, info, rebind, state))
        {
            if (!PortCompatibility.Matches((IGraphPort)anchorPort, (IGraphPort)myPort))
            {
                bindingConflicts++;
                continue;
            }

            WorldPort anchorWorld = SpatialPoseMath.WorldPortOf(anchorPose, anchorInfo.Footprint, anchorPort);
            for (int yaw = 0; yaw < 4; yaw++)
            {
                Vector3I? origin = SpatialPoseMath.SolveAbutment(anchorWorld, myPort, info.Footprint, (YawQuadrant)yaw);
                if (origin == null)
                {
                    bindingConflicts++;
                    continue;
                }

                var pose = new CellPose(origin.Value, (YawQuadrant)yaw);
                var bindings = new List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort)>
                {
                    (anchorEdge, myPort.NameOf(), anchorPort.NameOf()),
                };

                if (!TryCloseRemainingEdges(nodeId, pose, placedNeighborEdges, anchorEdge, bindings, rebind, info, infos, state))
                {
                    bindingConflicts++;
                    continue;
                }

                if (!PassesPlacementFilters(nodeId, pose, bindings, info, state, ref spaceConflicts, ref bindingConflicts))
                {
                    continue;
                }

                results.Add(new Candidate(pose, bindings));
            }
        }

        return results;
    }


    // A placed neighbour FROZEN before this pass began must keep the edge's DECLARED port, so a
    // committed loop/branch cannot consume a spare port that a later sub-graph needs. Neighbours placed
    // WITHIN this pass (route interiors) keep full rebind freedom; the set is empty for the holistic embed.
    private static IEnumerable<ISpatialPort> AnchorPortCandidates(
        IGraphEdge edge, StringName neighborId, NodeInfo neighborInfo, SearchState state)
    {
        if (state.FrozenNodes.Contains(neighborId))
        {
            StringName declared = edge.From.Id == neighborId ? edge.FromPort : edge.ToPort;
            return new[] { PortByName(neighborInfo, declared) };
        }

        return neighborInfo.Ports;
    }

    private static IEnumerable<(ISpatialPort AnchorPort, ISpatialPort MyPort)> PortPairs(
        IGraphEdge anchorEdge,
        StringName anchorId,
        StringName nodeId,
        NodeInfo anchorInfo,
        NodeInfo info,
        bool rebind,
        SearchState state)
    {
        if (!rebind)
        {
            (StringName anchorPortName, StringName myPortName) = anchorEdge.From.Id == anchorId
                ? (anchorEdge.FromPort, anchorEdge.ToPort)
                : (anchorEdge.ToPort, anchorEdge.FromPort);
            yield return (PortByName(anchorInfo, anchorPortName), PortByName(info, myPortName));
            yield break;
        }

        foreach (ISpatialPort anchorPort in AnchorPortCandidates(anchorEdge, anchorId, anchorInfo, state))
        {
            if (state.BoundPorts.Contains((anchorId, anchorPort.NameOf())) ||
                state.ReservedPorts.Contains((anchorId, anchorPort.NameOf())))
            {
                continue;
            }

            foreach (ISpatialPort myPort in info.Ports)
            {
                if (state.ReservedPorts.Contains((nodeId, myPort.NameOf())))
                {
                    continue;
                }

                yield return (anchorPort, myPort);
            }
        }
    }

    private static bool TryCloseRemainingEdges(
        StringName nodeId,
        CellPose pose,
        List<IGraphEdge> placedNeighborEdges,
        IGraphEdge anchorEdge,
        List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort)> bindings,
        bool rebind,
        NodeInfo info,
        Dictionary<StringName, NodeInfo> infos,
        SearchState state)
    {
        foreach (IGraphEdge edge in placedNeighborEdges)
        {
            if (ReferenceEquals(edge, anchorEdge))
            {
                continue;
            }

            StringName otherId = OtherEnd(edge, nodeId);
            NodeInfo otherInfo = infos[otherId];
            CellPose otherPose = state.Poses[otherId];
            bool closed = false;

            foreach ((ISpatialPort otherPort, ISpatialPort myPort) in ClosurePairs(edge, otherId, nodeId, otherInfo, info, rebind, state, bindings))
            {
                if (!PortCompatibility.Matches((IGraphPort)otherPort, (IGraphPort)myPort))
                {
                    continue;
                }

                WorldPort otherWorld = SpatialPoseMath.WorldPortOf(otherPose, otherInfo.Footprint, otherPort);
                Vector3I? required = SpatialPoseMath.SolveAbutment(otherWorld, myPort, info.Footprint, pose.Yaw);
                if (required != pose.Origin)
                {
                    continue;
                }

                bindings.Add((edge, myPort.NameOf(), otherPort.NameOf()));
                closed = true;
                break;
            }

            if (!closed)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<(ISpatialPort OtherPort, ISpatialPort MyPort)> ClosurePairs(
        IGraphEdge edge,
        StringName otherId,
        StringName nodeId,
        NodeInfo otherInfo,
        NodeInfo info,
        bool rebind,
        SearchState state,
        List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort)> bindings)
    {
        if (!rebind)
        {
            (StringName otherPortName, StringName myPortName) = edge.From.Id == otherId
                ? (edge.FromPort, edge.ToPort)
                : (edge.ToPort, edge.FromPort);
            yield return (PortByName(otherInfo, otherPortName), PortByName(info, myPortName));
            yield break;
        }

        var myUsed = bindings.Select(b => b.MyPort).ToHashSet();
        foreach (ISpatialPort otherPort in AnchorPortCandidates(edge, otherId, otherInfo, state))
        {
            if (state.BoundPorts.Contains((otherId, otherPort.NameOf())) ||
                state.ReservedPorts.Contains((otherId, otherPort.NameOf())))
            {
                continue;
            }

            foreach (ISpatialPort myPort in info.Ports)
            {
                if (myUsed.Contains(myPort.NameOf()) ||
                    state.ReservedPorts.Contains((nodeId, myPort.NameOf())))
                {
                    continue;
                }

                yield return (otherPort, myPort);
            }
        }
    }

    private static bool PassesPlacementFilters(
        StringName nodeId,
        CellPose pose,
        List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort)> bindings,
        NodeInfo info,
        SearchState state,
        ref int spaceConflicts,
        ref int bindingConflicts)
    {
        Vector3I size = SpatialPoseMath.RotateSize(info.Footprint, pose.Yaw);
        if (state.Occupancy.Overlaps(pose.Origin, size))
        {
            spaceConflicts++;
            return false;
        }

        (Vector3I min, Vector3I max) = CurrentBounds(state, (pose.Origin, size));
        Vector3I span = max - min;
        if (span.X > state.EnvelopeSize.X || span.Y > state.EnvelopeSize.Y || span.Z > state.EnvelopeSize.Z)
        {
            spaceConflicts++;
            return false;
        }

        if (!PassesResidualDegreePruning(nodeId, pose, bindings, info, state))
        {
            bindingConflicts++;
            return false;
        }

        return true;
    }

    private static bool PassesResidualDegreePruning(
        StringName nodeId,
        CellPose pose,
        List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort)> bindings,
        NodeInfo info,
        SearchState state)
    {
        var usedPorts = bindings.Select(b => b.MyPort).ToHashSet();
        int unboundRebindableEdges = 0;
        foreach (IGraphEdge edge in info.Edges)
        {
            if (state.EdgeBindings.ContainsKey(edge) || bindings.Any(b => ReferenceEquals(b.Edge, edge)))
            {
                continue;
            }

            StringName stagePort = edge.From.Id == nodeId ? edge.FromPort : edge.ToPort;
            if (state.ReservedPorts.Contains((nodeId, stagePort)))
            {
                // Tree edges keep their stage-1 binding: THAT exact port must stay exposable —
                // another free face is no substitute.
                if (OutwardSpanBlocked(pose, info.Footprint, PortByName(info, stagePort), state))
                {
                    return false;
                }

                continue;
            }

            unboundRebindableEdges++;
        }

        if (unboundRebindableEdges <= 0)
        {
            return true;
        }

        int usableFreePorts = 0;
        foreach (ISpatialPort port in info.Ports)
        {
            if (usedPorts.Contains(port.NameOf()) ||
                state.BoundPorts.Contains((nodeId, port.NameOf())) ||
                state.ReservedPorts.Contains((nodeId, port.NameOf())))
            {
                continue;
            }

            if (!OutwardSpanBlocked(pose, info.Footprint, port, state))
            {
                usableFreePorts++;
            }
        }

        return usableFreePorts >= unboundRebindableEdges;
    }

    private static bool OutwardSpanBlocked(CellPose pose, Vector3I footprint, ISpatialPort port, SearchState state)
    {
        WorldPort world = SpatialPoseMath.WorldPortOf(pose, footprint, port);
        Vector3I outwardStep = world.Face switch
        {
            PortFace.XPos => new Vector3I(0, 0, 0),
            PortFace.XNeg => new Vector3I(-1, 0, 0),
            PortFace.ZPos => new Vector3I(0, 0, 0),
            PortFace.ZNeg => new Vector3I(0, 0, -1),
            _ => throw new ArgumentException($"Concrete PortFace required; got {world.Face}.", nameof(port)),
        };
        Vector3I tangent = world.Face is PortFace.XPos or PortFace.XNeg ? new Vector3I(0, 0, 1) : new Vector3I(1, 0, 0);

        for (int t = 0; t < world.WidthCells; t++)
        {
            Vector3I cell = world.AnchorCells + outwardStep + (tangent * t);
            if (state.Occupancy.Overlaps(cell, new Vector3I(1, 1, 1)))
            {
                return true;
            }

            // A face pressed against the envelope boundary cannot expose an edge either: if even
            // one outward cell would push the layout's span past the envelope, no future neighbor
            // can ever abut here (coordinates are translation-relative, so this is a SIZE fold).
            // The candidate's own region is folded explicitly — it is not in state yet.
            Vector3I ownSize = SpatialPoseMath.RotateSize(footprint, pose.Yaw);
            (Vector3I min, Vector3I max) = CurrentBounds(state, (pose.Origin, ownSize));
            min = new Vector3I(Math.Min(min.X, cell.X), Math.Min(min.Y, cell.Y), Math.Min(min.Z, cell.Z));
            max = new Vector3I(Math.Max(max.X, cell.X + 1), Math.Max(max.Y, cell.Y + 1), Math.Max(max.Z, cell.Z + 1));
            Vector3I span = max - min;
            if (span.X > state.EnvelopeSize.X || span.Y > state.EnvelopeSize.Y || span.Z > state.EnvelopeSize.Z)
            {
                return true;
            }
        }

        return false;
    }

    private static (Vector3I Min, Vector3I Max) CurrentBounds(SearchState state, (Vector3I Origin, Vector3I Size)? infosRegionHint)
    {
        var min = new Vector3I(int.MaxValue, int.MaxValue, int.MaxValue);
        var max = new Vector3I(int.MinValue, int.MinValue, int.MinValue);

        void Fold(Vector3I origin, Vector3I size)
        {
            min = new Vector3I(Math.Min(min.X, origin.X), Math.Min(min.Y, origin.Y), Math.Min(min.Z, origin.Z));
            Vector3I end = origin + size;
            max = new Vector3I(Math.Max(max.X, end.X), Math.Max(max.Y, end.Y), Math.Max(max.Z, end.Z));
        }

        foreach ((StringName id, Vector3I origin, Vector3I size) in state.Regions)
        {
            Fold(origin, size);
        }

        if (infosRegionHint != null)
        {
            Fold(infosRegionHint.Value.Origin, infosRegionHint.Value.Size);
        }

        if (min.X == int.MaxValue)
        {
            return (Vector3I.Zero, Vector3I.Zero);
        }

        return (min, max);
    }

    private static void Apply(StringName nodeId, Candidate candidate, Dictionary<StringName, NodeInfo> infos, SearchState state)
    {
        NodeInfo info = infos[nodeId];
        Vector3I size = SpatialPoseMath.RotateSize(info.Footprint, candidate.Pose.Yaw);
        state.Poses[nodeId] = candidate.Pose;
        state.Occupancy.Add(nodeId, candidate.Pose.Origin, size);
        state.Regions.Add((nodeId, candidate.Pose.Origin, size));
        state.PlacementOrder.Add(nodeId);
        foreach ((IGraphEdge edge, StringName myPort, StringName otherPort) in candidate.Bindings)
        {
            StringName otherId = OtherEnd(edge, nodeId);
            state.BoundPorts.Add((nodeId, myPort));
            state.BoundPorts.Add((otherId, otherPort));
            state.EdgeBindings[edge] = edge.From.Id == nodeId ? (myPort, otherPort) : (otherPort, myPort);
        }
    }

    private static void Undo(StringName nodeId, Candidate candidate, Dictionary<StringName, NodeInfo> infos, SearchState state)
    {
        state.Poses.Remove(nodeId);
        state.Occupancy.Remove(nodeId);
        state.Regions.RemoveAll(r => r.Id == nodeId);
        state.PlacementOrder.RemoveAt(state.PlacementOrder.Count - 1);
        foreach ((IGraphEdge edge, StringName myPort, StringName otherPort) in candidate.Bindings)
        {
            StringName otherId = OtherEnd(edge, nodeId);
            state.BoundPorts.Remove((nodeId, myPort));
            state.BoundPorts.Remove((otherId, otherPort));
            state.EdgeBindings.Remove(edge);
        }
    }

    // ---- emission ----

    private static FloorEmbedResult EmitResult(IFloorGraph topology, Dictionary<StringName, NodeInfo> infos, SearchState state)
    {
        (Vector3I min, Vector3I _) = CurrentBounds(state, null);
        var layout = new Dictionary<StringName, CellPlacement>();
        foreach ((StringName id, CellPose pose) in state.Poses)
        {
            NodeInfo info = infos[id];
            layout[id] = new CellPlacement(pose.Origin - min, pose.Yaw, SpatialPoseMath.RotateSize(info.Footprint, pose.Yaw));
        }

        var doorways = new List<DoorwayPose>(topology.Edges.Count);
        foreach (IGraphEdge edge in topology.Edges)
        {
            (StringName fromPort, StringName toPort) = state.EdgeBindings[edge];
            NodeInfo fromInfo = infos[edge.From.Id];
            CellPose normalizedPose = new(state.Poses[edge.From.Id].Origin - min, state.Poses[edge.From.Id].Yaw);
            WorldPort fromWorld = SpatialPoseMath.WorldPortOf(normalizedPose, fromInfo.Footprint, PortByName(fromInfo, fromPort));
            doorways.Add(SpatialPoseMath.DeriveDoorway(edge.From.Id, edge.To.Id, fromPort, toPort, fromWorld));
        }

        return FloorEmbedResult.Success(layout, doorways);
    }

    // ---- shared helpers ----

    /// <summary>
    ///     Resolves the effective repair budget for a topology. With <paramref name="dynamic" /> off,
    ///     returns <paramref name="manualBudget" /> verbatim. With it on, scales by the floor's node +
    ///     loop-edge count (the two drivers of backjump cost — placements and closure constraints),
    ///     clamped to <c>[manualBudget, maxBudget]</c> and never below 1, so a degenerate (e.g.
    ///     negative) coefficient can never yield the non-positive budget the embedder rejects at entry.
    /// </summary>
    internal static int ResolveBudget(
        bool dynamic, int manualBudget, int budgetBase, int perNode, int perLoopEdge,
        int maxBudget, int nodeCount, int loopEdgeCount)
    {
        if (!dynamic)
        {
            return manualBudget;
        }

        long computed = (long)budgetBase + ((long)perNode * nodeCount) + ((long)perLoopEdge * loopEdgeCount);
        int floor = Math.Max(1, manualBudget);
        int ceiling = Math.Max(floor, maxBudget);
        return (int)Math.Clamp(computed, floor, ceiling);
    }

    // Endpoint-resolution now lives once, on the shared FloorGraphExtensions.NodeAcrossEdge query.
    // Every call site here passes a nodeId that is an endpoint of the edge (edges come from that
    // node's adjacency), so the non-null result is guaranteed.
    private static StringName OtherEnd(IGraphEdge edge, StringName nodeId)
        => edge.NodeAcrossEdge(nodeId)!.Id;

    private static ISpatialPort PortByName(NodeInfo info, StringName portName)
    {
        foreach (ISpatialPort port in info.Ports)
        {
            if (port.NameOf() == portName)
            {
                return port;
            }
        }

        throw new ArgumentException($"Node '{info.Node.Id}' has no port named '{portName}'.", nameof(portName));
    }

    private static ulong Fnv1A64(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (byte b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= prime;
        }

        return hash;
    }
}

/// <summary>
///     ISpatialPort intentionally omits Name (it lives on IGraphPort); embedder bookkeeping is
///     keyed by port name, so resolve it through the graph-port side of the dual-interface object.
/// </summary>
internal static class SpatialPortNameExtensions
{
    internal static StringName NameOf(this ISpatialPort port)
    {
        return ((IGraphPort)port).Name;
    }
}
