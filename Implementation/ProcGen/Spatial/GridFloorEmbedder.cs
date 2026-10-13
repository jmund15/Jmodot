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

    public FloorEmbedResult Embed(
        IFloorGraph topology,
        GeometryEnvelope envelope,
        EmbedderSettings settings,
        ConnectorRules rules)
    {
        ValidateEmbedArgs(topology, envelope, settings);
        var infos = BuildNodeInfos(topology);
        var state = NewState(topology, envelope, settings, rules);
        FloorEmbedResult? failure = PlaceAll(topology, infos, state);
        return Capture(failure ?? EmitResult(topology, infos, state), state);
    }

    /// <summary>
    ///     Opens a progressive embedding session: the returned <see cref="ILayoutAdvisor" /> embeds +
    ///     freezes <paramref name="backbone" /> immediately, then answers geometric queries and
    ///     trial-commits decorations for the generator. See <see cref="ILayoutAdvisor" />.
    /// </summary>
    public ILayoutAdvisor BeginSession(
        IFloorGraph backbone,
        GeometryEnvelope envelope,
        EmbedderSettings settings,
        ConnectorRules rules)
        => new LayoutSession(this, backbone, envelope, settings, rules);

    /// <summary>
    ///     Frozen-spine progressive seam (Design A): embeds a backbone sub-graph alone and returns its
    ///     populated <see cref="SearchState" /> — every backbone node carries a fixed pose + occupancy
    ///     unless the backbone does not fully place, in which case <see cref="SearchState.BackboneFailure" />
    ///     holds the typed failure and the state is a partial frame that <see cref="Extend" /> refuses.
    ///     Pair with <see cref="Extend" />, which resumes from this state to place loop/branch
    ///     decorations WITHOUT moving the frozen backbone (the placement order skips nodes already in
    ///     <c>state.Poses</c>). Lets the generator commit only geometry it has validated against the
    ///     real grid instead of re-rolling the whole topology on an embed miss.
    /// </summary>
    internal SearchState BuildState(
        IFloorGraph backbone,
        GeometryEnvelope envelope,
        EmbedderSettings settings,
        ConnectorRules rules)
    {
        ValidateEmbedArgs(backbone, envelope, settings);
        var infos = BuildNodeInfos(backbone);
        var state = NewState(backbone, envelope, settings, rules);
        state.BackboneFailure = PlaceAll(backbone, infos, state);
        return state;
    }

    /// <summary>
    ///     Resumes a frozen <paramref name="state" /> (from <see cref="BuildState" />) to embed the
    ///     FULL graph: backbone poses are reused unchanged (skipped by the placement order), only the
    ///     decoration nodes are searched, and the budget is re-sized to the full graph. Returns the
    ///     complete layout, or the typed failure if a decoration cannot be placed on the frozen frame. A state whose
    ///     backbone did not fully place returns that <see cref="SearchState.BackboneFailure" /> untouched.
    ///     <paramref name="fullGraph" /> MUST share node/edge INSTANCES with the backbone handed to
    ///     <see cref="BuildState" /> — edge bindings are identity-keyed, so a freshly-rebuilt graph
    ///     with equal ids but different instances loses the frozen bindings. The generator satisfies
    ///     this by construction (both are ToFloorGraph snapshots of the same growing PartialGraph).
    /// </summary>
    internal FloorEmbedResult Extend(
        SearchState state,
        IFloorGraph fullGraph,
        GeometryEnvelope envelope,
        EmbedderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.BackboneFailure.HasValue)
        {
            return state.BackboneFailure.Value;
        }

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

    private static SearchState NewState(
        IFloorGraph topology,
        GeometryEnvelope envelope,
        EmbedderSettings settings,
        ConnectorRules rules)
        => new SearchState(ResolveEffectiveBudget(topology, settings), envelope.SizeCells, rules);

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

    /// <summary>How many times the last <see cref="Embed" /> or <see cref="Extend" /> reached <see cref="ConnectorSolver.Solve" />.</summary>
    internal int LastConnectorSolveCalls { get; private set; }

    internal static bool OutwardSpanBlockedForTest(CellPose pose, Vector3I footprint, ISpatialPort port, SearchState state)
        => OutwardSpanBlocked(pose, footprint, port, state);

    /// <summary>The steering key of one candidate pose of <paramref name="nodeId" /> on <paramref name="state" />, as <c>GenerateCandidates</c> computes it.</summary>
    internal static int SteerDistanceForTest(
        IFloorGraph graph,
        StringName nodeId,
        IGraphEdge anchorEdge,
        CellPose pose,
        IReadOnlySet<StringName> boundByCandidate,
        SearchState state)
    {
        var infos = BuildNodeInfos(graph);
        return SteerDistance(SteerTargetsOf(nodeId, anchorEdge, infos, state), nodeId, pose, boundByCandidate, infos, state);
    }
#endif
#endregion

    private FloorEmbedResult Capture(FloorEmbedResult result, SearchState state)
    {
#if TOOLS
        this.LastPlacementTrace = state.PlacementOrder.ToList();
        this.LastConnectorSolveCalls = state.ConnectorSolveCalls;
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
        var options = new HashSet<Vector2I>();
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
                    foreach (Vector3I a in WindowAnchors(exit, info.Footprint, (YawQuadrant)yaw))
                    {
                        foreach (Vector3I b in WindowAnchors(entry, info.Footprint, (YawQuadrant)yaw))
                        {
                            options.Add(new Vector2I(a.X - b.X, a.Z - b.Z));
                        }
                    }
                }
            }
        }

        return options.ToList();
    }

    // The min-corner anchor of every door window a port admits, template-local after the yaw: each width in its range at
    // each start inside its run. A port whose run equals its width has one.
    private static IEnumerable<Vector3I> WindowAnchors(ISpatialPort port, Vector3I footprint, YawQuadrant yaw)
    {
        var pose = new CellPose(Vector3I.Zero, yaw);
        for (int width = port.MinWidthCells; width <= port.WidthCells; width++)
        {
            for (int offset = port.OffsetCells; offset + width <= port.OffsetCells + port.SpanCells; offset++)
            {
                yield return SpatialPoseMath.WindowOf(pose, footprint, port, new PortOpening(offset, width)).AnchorCells;
            }
        }
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
        internal SearchState(int budget, Vector3I envelopeSize, ConnectorRules rules)
        {
            this.Budget = budget;
            this.EnvelopeSize = envelopeSize;
            this.Rules = rules;
        }

        internal Dictionary<StringName, CellPose> Poses { get; } = new();

        internal OccupancyIndex Occupancy { get; private set; } = new();

        internal HashSet<(StringName NodeId, StringName PortName)> BoundPorts { get; } = new();

        internal Dictionary<IGraphEdge, (StringName FromPort, PortOpening FromOpening, StringName ToPort, PortOpening ToOpening)> EdgeBindings { get; } = new();

        internal Dictionary<IGraphEdge, ConnectorPath> PendingRealizations { get; } = new();

        internal List<(StringName Id, Vector3I Origin, Vector3I Size)> Regions { get; } = new();

        /// <summary>Stage-1 ports of tree edges — off-limits to block re-binding, since chains/leaves keep their stage-1 bindings.</summary>
        internal HashSet<(StringName NodeId, StringName PortName)> ReservedPorts { get; } = new();

        internal List<StringName> PlacementOrder { get; } = new();

#if TOOLS
        internal int ConnectorSolveCalls { get; set; }
#endif

        /// <summary>
        ///     Nodes already placed when the CURRENT placement pass began (populated by <see cref="Extend" />).
        ///     Their declared edge ports are HONORED during rebind, so a committed loop/branch cannot
        ///     cannibalize a spare port that a later sub-graph needs. Empty for the holistic
        ///     <see cref="Embed" /> and for <see cref="BuildState" /> (full rebind freedom — unchanged behavior).
        /// </summary>
        internal HashSet<StringName> FrozenNodes { get; } = new();

        internal int Budget { get; set; }

        internal ConnectorRules Rules { get; }

        internal Vector3I EnvelopeSize { get; }

        internal EmbedFailureCause LastConflictCause { get; set; } = EmbedFailureCause.NoBinding;

        /// <summary>
        ///     Set by <see cref="BuildState" /> when the backbone does not fully place; null for a complete frozen frame.
        /// </summary>
        internal FloorEmbedResult? BackboneFailure { get; set; }

        /// <summary>
        ///     Deep copy for trial placement: a candidate loop/branch is embedded onto the clone, which is
        ///     ADOPTED only if it succeeds — so a failed trial leaves the committed state pristine (no
        ///     residual poses, occupancy, bindings, or reserved ports to corrupt later placements).
        /// </summary>
        internal SearchState Clone()
        {
            var copy = new SearchState(this.Budget, this.EnvelopeSize, this.Rules)
            {
                Occupancy = this.Occupancy.Clone(),
                LastConflictCause = this.LastConflictCause,
                BackboneFailure = this.BackboneFailure,
            };
            foreach (var pose in this.Poses) { copy.Poses[pose.Key] = pose.Value; }
            foreach (var bound in this.BoundPorts) { copy.BoundPorts.Add(bound); }
            foreach (var binding in this.EdgeBindings) { copy.EdgeBindings[binding.Key] = binding.Value; }
            foreach (var realization in this.PendingRealizations) { copy.PendingRealizations[realization.Key] = realization.Value; }
            foreach (var reserved in this.ReservedPorts) { copy.ReservedPorts.Add(reserved); }
            foreach (var frozen in this.FrozenNodes) { copy.FrozenNodes.Add(frozen); }
            copy.Regions.AddRange(this.Regions);
            copy.PlacementOrder.AddRange(this.PlacementOrder);
#if TOOLS
            copy.ConnectorSolveCalls = this.ConnectorSolveCalls;
#endif
            return copy;
        }
    }

    private sealed class Candidate
    {
        internal Candidate(CellPose pose, List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort, PortOpening MyOpening, PortOpening OtherOpening)> bindings)
        {
            this.Pose = pose;
            this.Bindings = bindings;
        }

        internal CellPose Pose { get; }

        internal List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort, PortOpening MyOpening, PortOpening OtherOpening)> Bindings { get; }

        internal List<(IGraphEdge Edge, ConnectorPath Connector)> Realizations { get; } = new();
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

    // ---- route-room steering ----

    // A route room (every incident edge a Loop) prefers the candidate whose closures cost the least corridor,
    // then the candidate nearest the placed room its loop rejoins. Spine rooms keep enumeration order.

    // A rejoin door a candidate steers toward, and whether its room's port choice is frozen.
    private readonly record struct SteerTarget(ISpatialPort Port, WorldSpan Span, bool Frozen);

    // The pose-independent half of the steering key, computed once per GenerateCandidates.
    private static List<SteerTarget> SteerTargetsOf(
        StringName nodeId,
        IGraphEdge anchorEdge,
        Dictionary<StringName, NodeInfo> infos,
        SearchState state)
    {
        var targets = new List<SteerTarget>();
        foreach ((StringName targetId, IGraphEdge boundary) in RejoinBoundaries(nodeId, anchorEdge, infos, state))
        {
            NodeInfo targetInfo = infos[targetId];
            CellPose targetPose = state.Poses[targetId];
            bool frozen = state.FrozenNodes.Contains(targetId);
            IEnumerable<ISpatialPort> targetPorts = frozen
                ? new[] { PortByName(targetInfo, DeclaredPortOn(boundary, targetId)) }
                : targetInfo.Ports.Where(p => IsUnclaimed(targetId, p, state));
            foreach (ISpatialPort targetPort in targetPorts)
            {
                targets.Add(new SteerTarget(targetPort, SpatialPoseMath.SpanOf(targetPose, targetInfo.Footprint, targetPort), frozen));
            }
        }

        return targets;
    }

    /// <summary>
    ///     Steering distance of a candidate pose: the shortest <see cref="ConnectorSolver.EstimateBridgeCells" /> between a free
    ///     port of the room and a usable port of a placed room its unplaced component rejoins, so a door
    ///     facing the rejoin door beats one behind it. <see cref="int.MaxValue" /> when no such bridge exists.
    /// </summary>
    private static int SteerDistance(
        List<SteerTarget> targets,
        StringName nodeId,
        CellPose pose,
        IReadOnlySet<StringName> boundByCandidate,
        Dictionary<StringName, NodeInfo> infos,
        SearchState state)
    {
        if (targets.Count == 0)
        {
            return int.MaxValue;
        }

        NodeInfo info = infos[nodeId];
        var freeAnchors = info.Ports
            .Where(p => !boundByCandidate.Contains(p.NameOf()) && IsUnclaimed(nodeId, p, state))
            .Select(p => (Port: p, Span: SpatialPoseMath.SpanOf(pose, info.Footprint, p)))
            .ToList();

        int best = int.MaxValue;
        foreach (SteerTarget target in targets)
        {
            foreach ((ISpatialPort port, WorldSpan span) in freeAnchors)
            {
                if (!target.Frozen && !PortCompatibility.Matches((IGraphPort)target.Port, (IGraphPort)port))
                {
                    continue;
                }

                (WorldPort AWindow, WorldPort BWindow)? windows = SpatialPoseMath.ConnectorWindows(span, target.Span);
                if (!windows.HasValue)
                {
                    continue;
                }

                best = Math.Min(
                    best,
                    ConnectorSolver.EstimateBridgeCells(windows.Value.AWindow, windows.Value.BWindow, state.Rules.Policy) ?? int.MaxValue);
            }
        }

        return best;
    }

    private static bool IsUnclaimed(StringName nodeId, ISpatialPort port, SearchState state)
    {
        StringName name = port.NameOf();
        return !state.BoundPorts.Contains((nodeId, name)) && !state.ReservedPorts.Contains((nodeId, name));
    }

    // Placed rooms adjacent to the unplaced component containing nodeId, reached over every edge except the anchor.
    private static List<(StringName TargetId, IGraphEdge Boundary)> RejoinBoundaries(
        StringName nodeId,
        IGraphEdge anchorEdge,
        Dictionary<StringName, NodeInfo> infos,
        SearchState state)
    {
        var boundaries = new List<(StringName TargetId, IGraphEdge Boundary)>();
        var component = new HashSet<StringName> { nodeId };
        var queue = new Queue<StringName>();
        queue.Enqueue(nodeId);
        while (queue.Count > 0)
        {
            StringName current = queue.Dequeue();
            foreach (IGraphEdge edge in infos[current].Edges)
            {
                if (ReferenceEquals(edge, anchorEdge))
                {
                    continue;
                }

                StringName other = OtherEnd(edge, current);
                if (state.Poses.ContainsKey(other))
                {
                    boundaries.Add((other, edge));
                }
                else if (component.Add(other))
                {
                    queue.Enqueue(other);
                }
            }
        }

        return boundaries;
    }

    private static StringName DeclaredPortOn(IGraphEdge edge, StringName nodeId) =>
        edge.From.Id == nodeId ? edge.FromPort : edge.ToPort;

    private static int CorridorCells(Candidate candidate) => candidate.Realizations.Sum(r => r.Connector.LengthCells);

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
    ///     branch that caused the conflict instead of churning unrelated routes. A run of unplaced
    ///     ring nodes between placed ones is ordered ends-inward (see <see cref="EndsInward" />), so
    ///     the meeting gap of the run is the last thing closed. A ring with no placed node keeps the
    ///     ring-walk order.
    /// </summary>
    internal static List<StringName> BlockPlacementOrder(GraphBlock block, SearchState state)
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

            var rotated = new List<StringName>(ring.Count);
            for (int i = 0; i < ring.Count; i++)
            {
                rotated.Add(ring[(anchorIndex + i) % ring.Count]);
            }

            foreach (StringName id in EndsInward(rotated, cycle, state))
            {
                Append(id);
            }
        }

        foreach (IGraphNode node in block.Nodes)
        {
            Append(node.Id);
        }

        return order.Where(id => !state.Poses.ContainsKey(id)).ToList();
    }

    /// <summary>
    ///     Reorders each maximal run of unplaced nodes in <paramref name="ring" /> that has a placed node on both
    ///     sides (the same node may flank both) so the run is placed ends-inward: the end whose cycle edge points
    ///     <c>To</c> its placed neighbour (the rejoin side) first, then the opposite end, then alternating toward
    ///     the middle. When both ends or neither point <c>To</c> their neighbour, the end the ring walk reaches last
    ///     is the rejoin side. A run is emitted where its first member sits in the ring; a ring with no placed node
    ///     is returned unchanged.
    /// </summary>
    private static List<StringName> EndsInward(List<StringName> ring, IReadOnlyList<IGraphEdge> cycle, SearchState state)
    {
        int firstPlaced = ring.FindIndex(state.Poses.ContainsKey);
        if (firstPlaced < 0)
        {
            return ring;
        }

        var runOf = new Dictionary<StringName, List<StringName>>();
        StringName leftPlaced = ring[firstPlaced];
        var run = new List<StringName>();
        for (int step = 1; step <= ring.Count; step++)
        {
            StringName id = ring[(firstPlaced + step) % ring.Count];
            if (!state.Poses.ContainsKey(id))
            {
                run.Add(id);
                continue;
            }

            if (run.Count > 0)
            {
                List<StringName> sequence = OrderRunEndsInward(run, leftPlaced, id, cycle);
                foreach (StringName member in run)
                {
                    runOf[member] = sequence;
                }

                run = new List<StringName>();
            }

            leftPlaced = id;
        }

        var result = new List<StringName>(ring.Count);
        var emitted = new HashSet<List<StringName>>();
        foreach (StringName id in ring)
        {
            if (!runOf.TryGetValue(id, out List<StringName>? sequence))
            {
                result.Add(id);
            }
            else if (emitted.Add(sequence))
            {
                result.AddRange(sequence);
            }
        }

        return result;
    }

    private static List<StringName> OrderRunEndsInward(List<StringName> run, StringName leftPlaced, StringName rightPlaced, IReadOnlyList<IGraphEdge> cycle)
    {
        bool firstPointsTo = cycle.Any(e => e.From.Id == run[0] && e.To.Id == leftPlaced);
        bool lastPointsTo = cycle.Any(e => e.From.Id == run[^1] && e.To.Id == rightPlaced);
        bool rejoinIsFirst = firstPointsTo && !lastPointsTo;

        var sequence = new List<StringName>(run.Count);
        int low = 0;
        int high = run.Count - 1;
        bool takeRejoin = true;
        while (low <= high)
        {
            bool takeFirst = takeRejoin == rejoinIsFirst;
            sequence.Add(takeFirst ? run[low++] : run[high--]);
            takeRejoin = !takeRejoin;
        }

        return sequence;
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
                if (PassesPlacementFilters(nodeId, pose, new List<(IGraphEdge, StringName, StringName, PortOpening, PortOpening)>(), info, state, ref spaceConflicts, ref bindingConflicts))
                {
                    candidates.Add(new Candidate(pose, new List<(IGraphEdge, StringName, StringName, PortOpening, PortOpening)>()));
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

            if (info.Edges.IsRouteRoom())
            {
                List<SteerTarget> steerTargets = SteerTargetsOf(nodeId, anchorEdge, infos, state);
                candidates = candidates
                    .OrderBy(c => CorridorCells(c))
                    .ThenBy(c => SteerDistance(steerTargets, nodeId, c.Pose, c.Bindings.Select(b => b.MyPort).ToHashSet(), infos, state))
                    .ToList();
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

            WorldSpan anchorSpan = SpatialPoseMath.SpanOf(anchorPose, anchorInfo.Footprint, anchorPort);
            for (int yaw = 0; yaw < 4; yaw++)
            {
                bool anySlide = false;
                foreach (Vector3I origin in SpatialPoseMath.AbutmentSlides(anchorSpan, myPort, info.Footprint, (YawQuadrant)yaw))
                {
                    anySlide = true;
                    var pose = new CellPose(origin, (YawQuadrant)yaw);
                    WorldPort? window = SpatialPoseMath.SharedOpening(
                        anchorSpan, SpatialPoseMath.SpanOf(pose, info.Footprint, myPort));
                    if (window == null)
                    {
                        bindingConflicts++;
                        continue;
                    }

                    var bindings = new List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort, PortOpening MyOpening, PortOpening OtherOpening)>
                    {
                        (
                            anchorEdge,
                            myPort.NameOf(),
                            anchorPort.NameOf(),
                            SpatialPoseMath.ToOpening(window.Value, pose, info.Footprint, myPort),
                            SpatialPoseMath.ToOpening(window.Value, anchorPose, anchorInfo.Footprint, anchorPort)),
                    };

                    var realizations = new List<(IGraphEdge Edge, ConnectorPath Connector)>();
                    if (!TryCloseRemainingEdges(nodeId, pose, placedNeighborEdges, anchorEdge, bindings, realizations, rebind, info, infos, state))
                    {
                        bindingConflicts++;
                        continue;
                    }

                    if (!PassesPlacementFilters(nodeId, pose, bindings, info, state, ref spaceConflicts, ref bindingConflicts))
                    {
                        continue;
                    }

                    var candidate = new Candidate(pose, bindings);
                    candidate.Realizations.AddRange(realizations);
                    results.Add(candidate);
                }

                if (!anySlide)
                {
                    bindingConflicts++;
                }
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
        List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort, PortOpening MyOpening, PortOpening OtherOpening)> bindings,
        List<(IGraphEdge Edge, ConnectorPath Connector)> realizations,
        bool rebind,
        NodeInfo info,
        Dictionary<StringName, NodeInfo> infos,
        SearchState state)
    {
        Vector3I candidateSize = SpatialPoseMath.RotateSize(info.Footprint, pose.Yaw);
        var candidateLocalObstacles = new List<(Vector3I Origin, Vector3I Size)> { (pose.Origin, candidateSize) };
        foreach ((IGraphEdge _, ConnectorPath connector) in realizations)
        {
            candidateLocalObstacles.AddRange(connector.Boxes);
        }

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

                WorldSpan otherSpan = SpatialPoseMath.SpanOf(otherPose, otherInfo.Footprint, otherPort);
                WorldSpan mySpan = SpatialPoseMath.SpanOf(pose, info.Footprint, myPort);
                WorldPort? shared = SpatialPoseMath.SharedOpening(otherSpan, mySpan);
                if (shared != null)
                {
                    bindings.Add((
                        edge,
                        myPort.NameOf(),
                        otherPort.NameOf(),
                        SpatialPoseMath.ToOpening(shared.Value, pose, info.Footprint, myPort),
                        SpatialPoseMath.ToOpening(shared.Value, otherPose, otherInfo.Footprint, otherPort)));
                    closed = true;
                    break;
                }

                if (state.Rules.Policy == ConnectorPolicy.AbutmentOnly || edge.Provenance.Kind != EdgeProvenanceKind.Loop ||
                    !otherInfo.Template.CanRealizeConnectorAt(otherPort) || !info.Template.CanRealizeConnectorAt(myPort))
                {
                    continue;
                }

                (WorldPort OtherWindow, WorldPort MyWindow)? windows = SpatialPoseMath.ConnectorWindows(otherSpan, mySpan);
                if (!windows.HasValue)
                {
                    continue;
                }

                int maxLengthCells = state.Rules.MaxLengthCells > 0
                    ? state.Rules.MaxLengthCells
                    : Math.Max(state.EnvelopeSize.X, state.EnvelopeSize.Z);
                if (!ConnectorSolver.EstimateBridgeCells(windows.Value.OtherWindow, windows.Value.MyWindow, state.Rules.Policy, maxLengthCells).HasValue)
                {
                    continue;
                }

#if TOOLS
                state.ConnectorSolveCalls++;
#endif
                ConnectorPath? path = ConnectorSolver.Solve(
                    windows.Value.OtherWindow, windows.Value.MyWindow, state.Occupancy, candidateLocalObstacles, state.EnvelopeSize,
                    maxLengthCells, state.Rules.Policy);
                if (path == null)
                {
                    continue;
                }

                ConnectorPath connector = path.Value;

                // The placement filters span-checked the candidate BEFORE these boxes existed, and
                // only later nodes re-fold Regions — so the union of the committed floor, the
                // candidate, and the new boxes must be span-checked here or the last-realized
                // connector can push the layout past the envelope unnoticed.
                if (!RealizationFitsEnvelope(candidateLocalObstacles, connector.Boxes, state))
                {
                    continue;
                }

                bindings.Add((
                    edge,
                    myPort.NameOf(),
                    otherPort.NameOf(),
                    SpatialPoseMath.ToOpening(windows.Value.MyWindow, pose, info.Footprint, myPort),
                    SpatialPoseMath.ToOpening(windows.Value.OtherWindow, otherPose, otherInfo.Footprint, otherPort)));
                realizations.Add((edge, connector));
                candidateLocalObstacles.AddRange(connector.Boxes);
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
        List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort, PortOpening MyOpening, PortOpening OtherOpening)> bindings)
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
        List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort, PortOpening MyOpening, PortOpening OtherOpening)> bindings,
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
        List<(IGraphEdge Edge, StringName MyPort, StringName OtherPort, PortOpening MyOpening, PortOpening OtherOpening)> bindings,
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
        WorldSpan span = SpatialPoseMath.SpanOf(pose, footprint, port);
        Vector3I outwardStep = span.Face switch
        {
            PortFace.XPos => new Vector3I(0, 0, 0),
            PortFace.XNeg => new Vector3I(-1, 0, 0),
            PortFace.ZPos => new Vector3I(0, 0, 0),
            PortFace.ZNeg => new Vector3I(0, 0, -1),
            _ => throw new ArgumentException($"Concrete PortFace required; got {span.Face}.", nameof(port)),
        };
        Vector3I tangent = span.Face is PortFace.XPos or PortFace.XNeg ? new Vector3I(0, 0, 1) : new Vector3I(1, 0, 0);

        // A face pressed against the envelope boundary cannot expose an edge either: an outward cell that would push
        // the layout's span past the envelope can never take a neighbor (coordinates are translation-relative, so
        // this is a SIZE fold). The candidate's own region is folded explicitly — it is not in state yet.
        Vector3I ownSize = SpatialPoseMath.RotateSize(footprint, pose.Yaw);
        (Vector3I ownMin, Vector3I ownMax) = CurrentBounds(state, (pose.Origin, ownSize));
        int freeRun = 0;
        for (int t = 0; t < span.SpanCells; t++)
        {
            Vector3I cell = span.AnchorCells + outwardStep + (tangent * t);
            Vector3I min = new(Math.Min(ownMin.X, cell.X), Math.Min(ownMin.Y, cell.Y), Math.Min(ownMin.Z, cell.Z));
            Vector3I max = new(Math.Max(ownMax.X, cell.X + 1), Math.Max(ownMax.Y, cell.Y + 1), Math.Max(ownMax.Z, cell.Z + 1));
            Vector3I grown = max - min;
            bool blocked = state.Occupancy.Overlaps(cell, new Vector3I(1, 1, 1)) ||
                grown.X > state.EnvelopeSize.X || grown.Y > state.EnvelopeSize.Y || grown.Z > state.EnvelopeSize.Z;
            freeRun = blocked ? 0 : freeRun + 1;
            if (freeRun >= span.MinWidthCells)
            {
                return false;
            }
        }

        return true;
    }

    private static bool RealizationFitsEnvelope(
        IReadOnlyList<(Vector3I Origin, Vector3I Size)> candidateLocalObstacles,
        IReadOnlyList<(Vector3I Origin, Vector3I Size)> boxes,
        SearchState state)
    {
        (Vector3I min, Vector3I max) = CurrentBounds(state, null);
        foreach ((Vector3I origin, Vector3I size) in candidateLocalObstacles.Concat(boxes))
        {
            min = new Vector3I(Math.Min(min.X, origin.X), Math.Min(min.Y, origin.Y), Math.Min(min.Z, origin.Z));
            Vector3I end = origin + size;
            max = new Vector3I(Math.Max(max.X, end.X), Math.Max(max.Y, end.Y), Math.Max(max.Z, end.Z));
        }

        Vector3I span = max - min;
        return span.X <= state.EnvelopeSize.X && span.Y <= state.EnvelopeSize.Y && span.Z <= state.EnvelopeSize.Z;
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
        foreach ((IGraphEdge edge, StringName myPort, StringName otherPort, PortOpening myOpening, PortOpening otherOpening) in candidate.Bindings)
        {
            StringName otherId = OtherEnd(edge, nodeId);
            state.BoundPorts.Add((nodeId, myPort));
            state.BoundPorts.Add((otherId, otherPort));
            state.EdgeBindings[edge] = edge.From.Id == nodeId
                ? (myPort, myOpening, otherPort, otherOpening)
                : (otherPort, otherOpening, myPort, myOpening);
        }

        for (int i = 0; i < candidate.Realizations.Count; i++)
        {
            (IGraphEdge edge, ConnectorPath connector) = candidate.Realizations[i];
            StringName connectorId = RealizationId(edge, i);
            foreach ((Vector3I origin, Vector3I connectorSize) in connector.Boxes)
            {
                state.Occupancy.Add(connectorId, origin, connectorSize);
                state.Regions.Add((connectorId, origin, connectorSize));
            }

            state.PendingRealizations[edge] = connector;
        }
    }

    private static void Undo(StringName nodeId, Candidate candidate, Dictionary<StringName, NodeInfo> infos, SearchState state)
    {
        for (int i = 0; i < candidate.Realizations.Count; i++)
        {
            (IGraphEdge edge, ConnectorPath _) = candidate.Realizations[i];
            StringName connectorId = RealizationId(edge, i);
            state.Occupancy.Remove(connectorId);
            state.Regions.RemoveAll(r => r.Id == connectorId);
            state.PendingRealizations.Remove(edge);
        }

        state.Poses.Remove(nodeId);
        state.Occupancy.Remove(nodeId);
        state.Regions.RemoveAll(r => r.Id == nodeId);
        state.PlacementOrder.RemoveAt(state.PlacementOrder.Count - 1);
        foreach ((IGraphEdge edge, StringName myPort, StringName otherPort, PortOpening myOpening, PortOpening otherOpening) in candidate.Bindings)
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
        var connectors = new List<ConnectorRealization>();
        foreach (IGraphEdge edge in topology.Edges)
        {
            (StringName fromPort, PortOpening fromOpening, StringName toPort, PortOpening _) = state.EdgeBindings[edge];
            NodeInfo fromInfo = infos[edge.From.Id];
            CellPose normalizedPose = new(state.Poses[edge.From.Id].Origin - min, state.Poses[edge.From.Id].Yaw);
            WorldPort fromWorld = SpatialPoseMath.WindowOf(normalizedPose, fromInfo.Footprint, PortByName(fromInfo, fromPort), fromOpening);
            doorways.Add(SpatialPoseMath.DeriveDoorway(edge.From.Id, edge.To.Id, fromPort, toPort, fromWorld));

            if (!state.PendingRealizations.TryGetValue(edge, out ConnectorPath connector))
            {
                continue;
            }

            var placements = new List<CellPlacement>(connector.Boxes.Count);
            foreach ((Vector3I origin, Vector3I size) in connector.Boxes)
            {
                placements.Add(new CellPlacement(origin - min, YawQuadrant.Yaw0, size));
            }

            connectors.Add(new ConnectorRealization(edge.From.Id, edge.To.Id, fromPort, toPort, placements, connector.LengthCells));
        }

        return FloorEmbedResult.Success(layout, doorways, connectors);
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

    private static StringName RealizationId(IGraphEdge edge, int ordinal)
        => new($"conn_{edge.From.Id}_{edge.To.Id}_{ordinal}");

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
