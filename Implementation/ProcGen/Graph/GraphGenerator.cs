namespace Jmodot.Implementation.ProcGen.Graph;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.Shared;
using Jmodot.Implementation.Shared;

/// <summary>
///     The constructive floor-graph generator (P3a.6): turns an <see cref="ISkeletonConfig" /> + a
///     root seed into a deterministic, gate-aware Source→Sink topology with a per-node geometry layout.
///     A static utility (matching <c>PointCloudGenerator</c>) — every draw derives a distinct seeded
///     sub-stream from the floor seed, so the whole algorithm is RNG-free at the boundary and
///     re-runnable byte-for-byte.
///     <para>
///         Boundary purity (design §9/§10): the engine never reads back <see cref="ReservedRegion" />
///         geometry. Route closure is <b>topological</b> (a free, type-compatible port on the target);
///         the realizer's <see cref="INodeRealizer.TryReserve" /> veto is the sole placement-failure
///         signal, and <see cref="INodeRealizer.Release" /> the sole un-reserve channel during backtrack.
///     </para>
/// </summary>
public static class GraphGenerator
{
    // Config-promotable (no formal promote-machinery — matches PointCloudGenerator.MaxRejectionIterations).
    private const int MaxFloorAttempts = 16;
    private const int BacktrackBudget = 8;

    // ASCII unit separator — the same id-delimiter discipline PartialGraph / CandidateSlot use, so node
    // id segments can never be forged by id content.
    private const char Sep = (char)0x1F;

    public static GraphGenerationResult Generate(ISkeletonConfig config, int seedRoot, INodeRealizer realizer)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        if (realizer == null)
        {
            throw new ArgumentNullException(nameof(realizer));
        }

        config.Validate();

        var fatal = new Violation(
            ViolationKind.SpineInfeasible, Severity.Fatal,
            "Spine could not be laid within the floor-attempt budget.");

        for (int attempt = 0; attempt < MaxFloorAttempts; attempt++)
        {
            int floorSeed = SeedManager.DeriveChild(seedRoot, "floor", attempt.ToString());
            var state = new GenState(config, realizer, floorSeed);
            var outcome = state.BuildFloor(out var cause);
            if (outcome == FloorOutcome.Ok)
            {
                return state.ToResult(attempt + 1, succeeded: true);
            }

            state.ReleaseAll();
            if (outcome == FloorOutcome.PinUnsatisfiable)
            {
                // A pin can never become satisfiable by re-rolling — fail fast, do not exhaust attempts.
                return GenState.Failure(attempt + 1, cause);
            }

            fatal = cause;
        }

        return GenState.Failure(MaxFloorAttempts, fatal);
    }

    /// <summary>
    ///     Stable role-preference ordering: templates whose <see cref="INodeTemplate.Role" /> equals
    ///     <paramref name="preferred" /> come first, original pool order preserved within each group
    ///     (.NET <c>OrderBy</c> is a stable sort). Used to bias routing/branch passes toward Connector
    ///     templates without disturbing determinism. Exposed for direct unit testing of the sort.
    /// </summary>
    internal static IReadOnlyList<INodeTemplate> OrderByRolePreference(
        IReadOnlyList<INodeTemplate> templates, TemplateRole preferred)
        => templates.OrderBy(t => t.Role == preferred ? 0 : 1).ToList();

    private enum FloorOutcome
    {
        Ok,
        SpineInfeasible,
        PinUnsatisfiable,
    }

    /// <summary>
    ///     One floor-build attempt's mutable working set: the builder, the insertion-ordered geometry
    ///     layout, the per-node region index (for the <c>Near</c> forward-hint and for Release), and the
    ///     accumulated soft warnings. Re-created per attempt so a re-roll starts from a clean graph.
    /// </summary>
    private sealed class GenState
    {
        private readonly ISkeletonConfig _config;
        private readonly INodeRealizer _realizer;
        private readonly int _floorSeed;

        private readonly PartialGraph _g = new();
        private readonly List<(GraphNode Node, ReservedRegion Region)> _layout = new();
        private readonly Dictionary<StringName, ReservedRegion> _regionByNodeId = new();
        private readonly List<ReservedRegion> _occupied = new();
        private readonly List<Violation> _warnings = new();
        private readonly HashSet<string> _anchorReservedPorts = new();

        // Monotonic node-id ordinal — advances on every placement attempt (including a backtracked
        // retry), guaranteeing globally-unique, deterministic ids and letting a veto-by-id retry escape
        // the vetoed id.
        private int _ordinal;

        // Monotonic loop-route ordinal — captured once per LayRouteBetween call and stamped on every edge
        // that route commits, so distinct routes get distinct ordinals and shared-anchor rings stay
        // disambiguated. One counter threads BOTH loop passes (guaranteed + opportunistic) — a per-pass
        // restart would collide (Loop, 0). Distinct from _ordinal, which counts per-placement attempts.
        private int _routeOrdinal;

        public GenState(ISkeletonConfig config, INodeRealizer realizer, int floorSeed)
        {
            this._config = config;
            this._realizer = realizer;
            this._floorSeed = floorSeed;
        }

        private int BudgetMax => this._config.NodeBudget?.Max ?? int.MaxValue;

        public FloorOutcome BuildFloor(out Violation cause)
        {
            cause = default;

            var spineOutcome = this.LaySpine(out cause);
            if (spineOutcome != FloorOutcome.Ok)
            {
                return spineOutcome;
            }

            if (!this.LayGuaranteedLoops(out cause))
            {
                return FloorOutcome.SpineInfeasible; // a guaranteed loop could not be embedded — re-roll
            }

            this.LayOpportunisticRoutes();
            this.LayBranches();

            int budgetMin = this._config.NodeBudget?.Min ?? 0;
            if (this._g.NodeCount < budgetMin)
            {
                this._warnings.Add(new Violation(
                    ViolationKind.BudgetUnfilled, Severity.Warning,
                    $"Floor laid {this._g.NodeCount} nodes, under NodeBudget.Min ({budgetMin})."));
            }

            return FloorOutcome.Ok;
        }

        // ── Spine ───────────────────────────────────────────────────────────

        private FloorOutcome LaySpine(out Violation cause)
        {
            cause = default;

            IntRange? lengthSpec = this._config.Spine?.Length;
            int length = this.DrawCount(lengthSpec, fallback: 3, passKey: "spine", tag: "length");
            if (length < 1)
            {
                length = 1;
            }

            var weights = this._config.Spine?.EffectiveWeights ?? (IReadOnlyList<SlotWeight>)Array.Empty<SlotWeight>();
            var constraints = this._config.Spine?.EffectiveConstraints ?? (IReadOnlyList<SlotConstraint>)Array.Empty<SlotConstraint>();
            Dictionary<int, INodeTemplate> forcedByIndex = this.ResolveForcedTemplates(length);

            INodeTemplate? forcedFirst = forcedByIndex.GetValueOrDefault(0);
            GraphNode? first = this.PlaceNode("spine", near: null, requiredType: default, anchor: null, weights, constraints, forcedFirst);
            if (first == null)
            {
                cause = forcedFirst != null
                    ? PinUnsatisfiable("the Source pin's template could not be placed.")
                    : SpineInfeasible("no admissible template for the spine source.");
                return forcedFirst != null ? FloorOutcome.PinUnsatisfiable : FloorOutcome.SpineInfeasible;
            }

            this._g.SetSource(first);
            GraphNode prev = first;
            int placed = 1;

            for (int i = 1; i < length; i++)
            {
                if (this._g.NodeCount >= this.BudgetMax)
                {
                    break; // live node-budget ceiling
                }

                IGraphPort? exit = this.SelectOpenPort(prev, requiredType: default);
                if (exit == null)
                {
                    break; // prev has no spare port to grow from — stop the spine here
                }

                INodeTemplate? forced = forcedByIndex.GetValueOrDefault(i);
                GraphNode? node = this.PlaceNode(
                    "spine", near: this._regionByNodeId[prev.Id], requiredType: exit.Type,
                    anchor: new PortSlot(prev, exit), weights, constraints, forced);
                if (node == null)
                {
                    cause = forced != null
                        ? PinUnsatisfiable($"the pin at spine index {i} could not be placed.")
                        : SpineInfeasible($"no admissible template for spine node {i}.");
                    return forced != null ? FloorOutcome.PinUnsatisfiable : FloorOutcome.SpineInfeasible;
                }

                IGraphPort? entry = this.SelectOpenPort(node, requiredType: exit.Type);
                if (entry == null)
                {
                    cause = SpineInfeasible($"spine node {i} exposes no port compatible with its predecessor.");
                    return FloorOutcome.SpineInfeasible;
                }

                this._g.Connect(prev, exit, node, entry, provenance: new EdgeProvenance(EdgeProvenanceKind.Spine, 0));
                prev = node;
                placed++;
            }

            // A truncated spine (budget ceiling / port exhaustion) is an accepted degradation — unless
            // it strands an authored pin (incl. the SinkPin mapped at the drawn tail). Silent pin loss
            // is never acceptable; fail fast as PinUnsatisfiable, consistent with every other pin path.
            foreach (int idx in forcedByIndex.Keys.OrderBy(k => k))
            {
                if (idx >= placed)
                {
                    cause = PinUnsatisfiable($"spine truncated at {placed} nodes before reaching the pin at index {idx}.");
                    return FloorOutcome.PinUnsatisfiable;
                }
            }

            this._g.SetSink(prev);
            return FloorOutcome.Ok;
        }

        // ── Guaranteed loops (backbone co-planning) ─────────────────────────

        private bool LayGuaranteedLoops(out Violation cause)
        {
            cause = default;
            AlternateRouteSpec? spec = this._config.AlternateRoutes;
            if (spec == null)
            {
                return true;
            }

            int guaranteed = this.DrawCount(spec.GuaranteedCount, fallback: 0, "guaranteed", "count");
            if (guaranteed <= 0)
            {
                return true;
            }

            int minSep = spec.MinAnchorSeparation;
            List<AnchorPair> pairs = this.PickAnchorPairs("guaranteed", guaranteed, minSep, spec.EffectiveAttachmentWeights);

            foreach (AnchorPair pair in pairs)
            {
                if (!this.LayRouteBetween(pair, spec, "guaranteed"))
                {
                    cause = new Violation(
                        ViolationKind.SpineInfeasible, Severity.Fatal,
                        "A guaranteed alternate route could not be embedded after backtracking.");
                    return false;
                }
            }

            if (pairs.Count < guaranteed)
            {
                // Fewer eligible anchor pairs than requested — surfaced as a soft warning (the floor is
                // still a valid connected topology; the backbone-feasibility Validate gate guards the
                // authored config, but a sampled short spine can still under-fill).
                this._warnings.Add(new Violation(
                    ViolationKind.AlternateRoutesUnfilled, Severity.Warning,
                    $"Embedded {pairs.Count} guaranteed loops; {guaranteed} requested."));
            }

            return true;
        }

        private readonly struct AnchorPair
        {
            public AnchorPair(GraphNode x, IGraphPort xPort, GraphNode y, IGraphPort yPort)
            {
                this.X = x;
                this.XPort = xPort;
                this.Y = y;
                this.YPort = yPort;
            }

            public GraphNode X { get; }
            public IGraphPort XPort { get; }
            public GraphNode Y { get; }
            public IGraphPort YPort { get; }
        }

        /// <summary>
        ///     Selects up to <paramref name="count" /> divergence/rejoin anchor pairs from the spine
        ///     interior. Eligibility: X≠Source, Y≠Sink, X≺Y, DistanceFromSource(X)+minSep ≤
        ///     DistanceFromSource(Y), both with a spare port. Pairs are ordered deterministically by
        ///     (dist X, dist Y, X.Id, Y.Id); selection is EndpointWeight-biased via a seeded weighted
        ///     pick, and each pick consumes a spare port on X and Y so later pairs see the reduced pool.
        ///     <paramref name="passKey" /> discriminates the RNG sub-stream per calling pass, so the
        ///     guaranteed and opportunistic passes never share a draw stream.
        /// </summary>
        private List<AnchorPair> PickAnchorPairs(string passKey, int count, int minSep, IReadOnlyList<EndpointWeight> weights)
        {
            var result = new List<AnchorPair>();
            if (!this._g.TryGetMetrics(out IGraphMetrics metrics))
            {
                return result; // no spine ⇒ no anchors (defensive; spine is laid first)
            }

            for (int pick = 0; pick < count; pick++)
            {
                List<(GraphNode X, GraphNode Y)> eligible = this.EnumerateEligiblePairs(metrics, minSep);
                if (eligible.Count == 0)
                {
                    break;
                }

                var choices = new List<((GraphNode X, GraphNode Y) Pair, long Weight)>(eligible.Count);
                foreach ((GraphNode X, GraphNode Y) p in eligible)
                {
                    choices.Add((p, this.EndpointWeightProduct(p, weights)));
                }

                var rng = new JmoRng(SeedManager.DeriveChild(this._floorSeed, passKey, "anchors", pick.ToString()));
                long total = WeightedPick.TotalWeight(choices);
                (GraphNode X, GraphNode Y) chosen = WeightedPick.Pick(choices, rng.GetRndLong(total));

                IGraphPort? xPort = this.SelectAnchorPort(chosen.X);
                IGraphPort? yPort = this.SelectAnchorPort(chosen.Y);
                if (xPort == null || yPort == null)
                {
                    break; // ports exhausted between enumeration and reservation — stop
                }

                this._anchorReservedPorts.Add(PortKey(chosen.X.Id, xPort.Name));
                this._anchorReservedPorts.Add(PortKey(chosen.Y.Id, yPort.Name));
                result.Add(new AnchorPair(chosen.X, xPort, chosen.Y, yPort));
            }

            return result;
        }

        // Metrics are guaranteed live here (PickAnchorPairs early-returns without them), so
        // RequiresMetrics endpoint rules are always active — no gating, unlike the pre-Sink
        // placement path in PlacementWeightProduct.
        private long EndpointWeightProduct((GraphNode X, GraphNode Y) pair, IReadOnlyList<EndpointWeight> weights)
        {
            long w = 1;
            foreach (EndpointWeight ew in weights)
            {
                w *= ew.Weight(pair.X, EndpointRole.Divergence, this._g);
                w *= ew.Weight(pair.Y, EndpointRole.Rejoin, this._g);
            }

            // Generator-side clamp: WeightedPick rejects a zero total, and a neutral weight is 1.
            return Math.Max(1L, w);
        }

        private List<(GraphNode X, GraphNode Y)> EnumerateEligiblePairs(IGraphMetrics metrics, int minSep)
        {
            var nodes = this._g.Nodes;
            var withSpare = nodes
                .Where(n => this.SelectAnchorPort(n) != null)
                .ToList();

            var pairs = new List<(GraphNode X, GraphNode Y)>();
            foreach (GraphNode x in withSpare)
            {
                if (this._g.Source != null && x.Id == this._g.Source.Id)
                {
                    continue; // X ≠ Source
                }

                int dx = metrics.DistanceFromSource(x);
                foreach (GraphNode y in withSpare)
                {
                    if (this._g.Sink != null && y.Id == this._g.Sink.Id)
                    {
                        continue; // Y ≠ Sink
                    }

                    int dy = metrics.DistanceFromSource(y);
                    if (dx + minSep <= dy) // implies X ≺ Y and min-separation
                    {
                        pairs.Add((x, y));
                    }
                }
            }

            // Deterministic order: (dist X, dist Y, X.Id, Y.Id).
            pairs.Sort((a, b) =>
            {
                int c = metrics.DistanceFromSource(a.X).CompareTo(metrics.DistanceFromSource(b.X));
                if (c != 0)
                {
                    return c;
                }

                c = metrics.DistanceFromSource(a.Y).CompareTo(metrics.DistanceFromSource(b.Y));
                if (c != 0)
                {
                    return c;
                }

                c = string.CompareOrdinal(a.X.Id.ToString(), b.X.Id.ToString());
                return c != 0 ? c : string.CompareOrdinal(a.Y.Id.ToString(), b.Y.Id.ToString());
            });

            return pairs;
        }

        /// <summary>
        ///     Lays a route of routing nodes from <c>pair.X</c> and closes onto <c>pair.Y</c>. Reserves
        ///     the whole chain provisionally (PartialGraph has no node removal, so commit is deferred);
        ///     a <see cref="INodeRealizer.TryReserve" /> veto triggers Release of the last reserved node
        ///     and a fresh-id retry, bounded by <see cref="BacktrackBudget" />. Commits AddNode + Connect
        ///     only once the full chain reserves cleanly; closure onto Y is topological (Y's reserved
        ///     spare port). Returns false if the route cannot be embedded within the backtrack budget.
        /// </summary>
        private bool LayRouteBetween(AnchorPair pair, AlternateRouteSpec spec, string passKey)
        {
            // Captured once per call (before any early return) so a dangling partial commit still consumes
            // its unique ordinal — harmless, and keeps every committed route's ordinal route-unique.
            int routeOrdinal = this._routeOrdinal++;

            int routeLen = this.DrawCount(spec.Length, fallback: 1, passKey, "len" + pair.X.Id);
            if (routeLen < 1)
            {
                routeLen = 1;
            }

            var prov = new List<(StringName Id, INodeTemplate Template, ReservedRegion Region)>();
            int backtrackUsed = 0;
            StringName entryType = pair.XPort.Type;

            while (prov.Count < routeLen)
            {
                if (this._g.NodeCount + prov.Count >= this.BudgetMax)
                {
                    break; // live ceiling — close the route early with what is laid so far
                }

                int ord = this._ordinal++;
                var id = new StringName($"{passKey}{Sep}{ord}");
                ReservedRegion? near = prov.Count > 0 ? prov[^1].Region : this._regionByNodeId[pair.X.Id];

                (INodeTemplate Template, ReservedRegion Region)? reserved =
                    this.ReserveFor(id, entryType, anchor: null, Array.Empty<SlotWeight>(), Array.Empty<SlotConstraint>(), near, passKey, ord, forced: null, pref: TemplateRole.Connector);

                if (reserved != null)
                {
                    prov.Add((id, reserved.Value.Template, reserved.Value.Region));
                    this._occupied.Add(reserved.Value.Region);
                    continue;
                }

                // Veto-exhaustion for this id — backtrack: release the last reserved node and retry with
                // a fresh id (the ordinal already advanced, so the next attempt escapes a veto-by-id).
                if (backtrackUsed >= BacktrackBudget)
                {
                    this.ReleaseProvisional(prov);
                    return false;
                }

                backtrackUsed++;
                if (prov.Count > 0)
                {
                    var last = prov[^1];
                    this._realizer.Release(in last.Region);
                    this._occupied.Remove(last.Region);
                    prov.RemoveAt(prov.Count - 1);
                }
            }

            if (prov.Count == 0)
            {
                return false; // budget left no room for even one routing node
            }

            return this.CommitRoute(pair, prov, routeOrdinal);
        }

        private bool CommitRoute(AnchorPair pair, List<(StringName Id, INodeTemplate Template, ReservedRegion Region)> prov, int routeOrdinal)
        {
            var nodes = new List<GraphNode>(prov.Count);
            foreach (var step in prov)
            {
                GraphNode n = this._g.AddNode(step.Id, step.Template);
                this._layout.Add((n, step.Region));
                this._regionByNodeId[step.Id] = step.Region;
                nodes.Add(n);
            }

            GraphNode prevNode = pair.X;
            IGraphPort prevPort = pair.XPort;
            foreach (GraphNode n in nodes)
            {
                IGraphPort? entry = this.SelectOpenPort(n, prevPort.Type);
                if (entry == null)
                {
                    return false; // route node exposes no compatible entry port (config error)
                }

                this._g.Connect(prevNode, prevPort, n, entry, provenance: new EdgeProvenance(EdgeProvenanceKind.Loop, routeOrdinal));
                prevNode = n;
                IGraphPort? exit = this.SelectOpenPort(n, requiredType: default);
                if (exit == null)
                {
                    return false; // no spare exit to continue / close
                }

                prevPort = exit;
            }

            this._g.Connect(prevNode, prevPort, pair.Y, pair.YPort, provenance: new EdgeProvenance(EdgeProvenanceKind.Loop, routeOrdinal)); // topological closure onto Y
            return true;
        }

        private void ReleaseProvisional(List<(StringName Id, INodeTemplate Template, ReservedRegion Region)> prov)
        {
            foreach (var step in prov)
            {
                this._realizer.Release(in step.Region);
                this._occupied.Remove(step.Region);
            }

            prov.Clear();
        }

        private Dictionary<int, INodeTemplate> ResolveForcedTemplates(int length)
        {
            var map = new Dictionary<int, INodeTemplate>();
            SpineSpec? spine = this._config.Spine;
            if (spine?.SourcePin?.AsNodeTemplate is INodeTemplate src)
            {
                map[0] = src;
            }

            if (length > 0 && spine?.SinkPin?.AsNodeTemplate is INodeTemplate snk)
            {
                map[length - 1] = snk;
            }

            foreach (PinnedPlacement pin in this._config.Pins)
            {
                if (pin?.Anchor == null)
                {
                    continue;
                }

                int idx = pin.Anchor.ResolveSpineIndex(this._config);
                if (idx >= 0 && idx < length && pin.AsNodeTemplate is INodeTemplate t)
                {
                    map[idx] = t; // interior pins win over the endpoint defaults at a shared index
                }
            }

            return map;
        }

        // ── Opportunistic routes (best-effort decoration) ───────────────────

        private void LayOpportunisticRoutes()
        {
            AlternateRouteSpec? spec = this._config.AlternateRoutes;
            if (spec == null)
            {
                return;
            }

            int opportunistic = this.DrawCount(spec.OpportunisticCount, fallback: 0, "opportunistic", "count");
            if (opportunistic <= 0)
            {
                return;
            }

            List<AnchorPair> pairs = this.PickAnchorPairs("opportunistic", opportunistic, spec.MinAnchorSeparation, spec.EffectiveAttachmentWeights);
            int embedded = 0;
            foreach (AnchorPair pair in pairs)
            {
                if (this._g.NodeCount >= this.BudgetMax)
                {
                    break;
                }

                if (this.LayRouteBetween(pair, spec, "opportunistic"))
                {
                    embedded++;
                }

                // else: soft-skip — an opportunistic route that cannot close is simply dropped.
            }

            int requestedMin = spec.OpportunisticCount?.Min ?? 0;
            if (embedded < requestedMin)
            {
                this._warnings.Add(new Violation(
                    ViolationKind.AlternateRoutesUnfilled, Severity.Warning,
                    $"Embedded {embedded} opportunistic routes; {requestedMin} requested."));
            }
        }

        // ── Branches (dead-end offshoots) ───────────────────────────────────

        private void LayBranches()
        {
            BranchSpec? spec = this._config.Branching;
            if (spec == null)
            {
                return;
            }

            int count = this.DrawCount(spec.Count, fallback: 0, "branch", "count");
            if (count <= 0)
            {
                return;
            }

            int depth = this.DrawCount(spec.Depth, fallback: 1, "branch", "depth");
            int fanout = this.DrawCount(spec.FanOut, fallback: 1, "branch", "fanout");
            var weights = spec.EffectiveWeights;
            var constraints = spec.EffectiveConstraints;

            for (int b = 0; b < count; b++)
            {
                GraphNode? anchor = this.PickBranchAnchor();
                if (anchor == null)
                {
                    break; // no node has a spare port — stop branching
                }

                this.GrowBranch(anchor, depth, fanout, weights, constraints, b);
            }
        }

        // rootOrdinal = the b-th branch growth (the LayBranches loop index); the whole tree under this
        // root shares it, even when a branch roots on a node an earlier branch created.
        private void GrowBranch(
            GraphNode parent, int depth, int fanout,
            IReadOnlyList<SlotWeight> weights, IReadOnlyList<SlotConstraint> constraints, int rootOrdinal)
        {
            if (depth <= 0)
            {
                return;
            }

            for (int f = 0; f < fanout; f++)
            {
                if (this._g.NodeCount >= this.BudgetMax)
                {
                    return; // live ceiling
                }

                IGraphPort? exit = this.SelectOpenPort(parent, requiredType: default);
                if (exit == null)
                {
                    return; // parent exhausted its spare ports
                }

                GraphNode? child = this.PlaceNode(
                    "branch", near: this._regionByNodeId[parent.Id], requiredType: exit.Type,
                    anchor: new PortSlot(parent, exit), weights, constraints, forced: null, pref: TemplateRole.Connector);
                if (child == null)
                {
                    return; // veto-exhausted — soft-skip the rest of this branch
                }

                IGraphPort? entry = this.SelectOpenPort(child, exit.Type);
                if (entry == null)
                {
                    return;
                }

                this._g.Connect(parent, exit, child, entry, provenance: new EdgeProvenance(EdgeProvenanceKind.Branch, rootOrdinal));
                this.GrowBranch(child, depth - 1, fanout, weights, constraints, rootOrdinal);
            }
        }

        private GraphNode? PickBranchAnchor()
        {
            foreach (GraphNode node in this._g.Nodes)
            {
                if (this.SelectOpenPort(node, requiredType: default) != null)
                {
                    return node;
                }
            }

            return null;
        }

        // ── Placement primitives ────────────────────────────────────────────

        /// <summary>
        ///     Commits a new node into the spine: reserves a region (re-picking on a veto) then AddNode +
        ///     layout record. Returns null when no template is constraint-admissible or every candidate
        ///     is vetoed. Used commit-as-you-go for the spine (a mid-spine failure re-rolls the floor).
        /// </summary>
        private GraphNode? PlaceNode(
            string passKey, ReservedRegion? near, StringName requiredType,
            PortSlot? anchor, IReadOnlyList<SlotWeight> weights, IReadOnlyList<SlotConstraint> constraints,
            INodeTemplate? forced = null, TemplateRole pref = TemplateRole.Body)
        {
            int ord = this._ordinal++;
            var nodeId = new StringName($"{passKey}{Sep}{ord}");

            (INodeTemplate Template, ReservedRegion Region)? reserved =
                this.ReserveFor(nodeId, requiredType, anchor, weights, constraints, near, passKey, ord, forced, pref);
            if (reserved == null)
            {
                return null;
            }

            GraphNode node = this._g.AddNode(nodeId, reserved.Value.Template);
            this._layout.Add((node, reserved.Value.Region));
            this._regionByNodeId[nodeId] = reserved.Value.Region;
            this._occupied.Add(reserved.Value.Region);
            return node;
        }

        /// <summary>
        ///     Resolves a template for <paramref name="nodeId" /> and reserves its region. Hard-filters
        ///     the pool by port-type compatibility + constraints, weighted-draws a candidate, and on a
        ///     realizer veto removes it and re-picks. Returns the chosen (template, region), or null on an
        ///     empty candidate set or veto-exhaustion. Does NOT touch the graph — caller commits.
        /// </summary>
        private (INodeTemplate Template, ReservedRegion Region)? ReserveFor(
            StringName nodeId, StringName requiredType, PortSlot? anchor,
            IReadOnlyList<SlotWeight> weights, IReadOnlyList<SlotConstraint> constraints,
            ReservedRegion? near, string passKey, int ordinal,
            INodeTemplate? forced = null, TemplateRole pref = TemplateRole.Body)
        {
            List<INodeTemplate> candidates;
            if (forced != null)
            {
                // A pin overrides free selection (and the constraint filter — it is authored intent);
                // it still requires a port compatible with its predecessor.
                candidates = HasOpenTypeMatch(forced, requiredType)
                    ? new List<INodeTemplate> { forced }
                    : new List<INodeTemplate>();
            }
            else
            {
                candidates = OrderByRolePreference(
                    this._config.TemplatePool
                        .Where(t => HasOpenTypeMatch(t, requiredType))
                        .Where(t => this.PassesConstraints(t, anchor, constraints))
                        .ToList(),
                    pref).ToList();
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            while (candidates.Count > 0)
            {
                INodeTemplate t = this.WeightedDraw(candidates, passKey, nodeId, ordinal, anchor, weights);
                var request = new ReserveRequest(t, nodeId, near);
                if (this._realizer.TryReserve(in request, this._occupied, out var region))
                {
                    return (t, region);
                }

                candidates.Remove(t); // veto → re-pick a different template
            }

            return null; // veto-exhaustion
        }

        private INodeTemplate WeightedDraw(
            List<INodeTemplate> candidates, string passKey, StringName nodeId, int ordinal,
            PortSlot? anchor, IReadOnlyList<SlotWeight> weights)
        {
            bool hasMetrics = this._g.TryGetMetrics(out _);
            var choices = new List<(INodeTemplate Item, long Weight)>(candidates.Count);
            foreach (INodeTemplate t in candidates)
            {
                choices.Add((t, this.PlacementWeightProduct(anchor, t, weights, hasMetrics)));
            }

            var rng = new JmoRng(SeedManager.DeriveChild(
                this._floorSeed, passKey, "pick", nodeId.ToString(), ordinal.ToString()));
            long total = WeightedPick.TotalWeight(choices);
            long roll = rng.GetRndLong(total);
            return WeightedPick.Pick(choices, roll);
        }

        private long PlacementWeightProduct(PortSlot? anchor, INodeTemplate t, IReadOnlyList<SlotWeight> weights, bool hasMetrics)
        {
            if (anchor == null)
            {
                return 1; // no placement context (spine source / provisional route node) — neutral
            }

            long product = 1;
            var placement = new Placement(anchor, t);
            foreach (SlotWeight w in this._config.GlobalWeights.Concat(weights))
            {
                if (w.RequiresMetrics && !hasMetrics)
                {
                    continue; // metrics snapshot not yet live (pre-Sink) — rule inactive
                }

                product *= w.Weight(in placement, this._g);
            }

            // Generator-side clamp: WeightedPick rejects a zero total, and a neutral weight is 1.
            return Math.Max(1L, product);
        }

        private bool PassesConstraints(INodeTemplate t, PortSlot? anchor, IReadOnlyList<SlotConstraint> constraints)
        {
            if (anchor == null)
            {
                return true; // no placement context (spine source / provisional route node)
            }

            bool hasMetrics = this._g.TryGetMetrics(out _);
            var placement = new Placement(anchor, t);
            foreach (SlotConstraint c in this._config.GlobalConstraints.Concat(constraints))
            {
                if (c.RequiresMetrics && !hasMetrics)
                {
                    continue; // metrics-required constraint inactive pre-Sink
                }

                if (!c.IsAdmissible(in placement, this._g))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     First open, type-compatible port on <paramref name="node" /> in template order. A port is
        ///     open when no edge references it; types match when equal OR either side is the empty
        ///     wildcard (Decision E). Null when the node exposes no compatible spare port.
        /// </summary>
        private IGraphPort? SelectOpenPort(GraphNode node, StringName requiredType)
        {
            foreach (IGraphPort port in node.Template.Ports)
            {
                if (this.IsPortOpen(node.Id, port.Name) && TypeMatches(port.Type, requiredType))
                {
                    return port;
                }
            }

            return null;
        }

        // An open port not already tentatively reserved for an anchor (so two loops can't claim one port).
        private IGraphPort? SelectAnchorPort(GraphNode node)
        {
            foreach (IGraphPort port in node.Template.Ports)
            {
                if (this.IsPortOpen(node.Id, port.Name) && !this._anchorReservedPorts.Contains(PortKey(node.Id, port.Name)))
                {
                    return port;
                }
            }

            return null;
        }

        private bool IsPortOpen(StringName nodeId, StringName portName)
        {
            foreach (GraphEdge e in this._g.Edges)
            {
                if ((e.From.Id == nodeId && e.FromPort == portName) ||
                    (e.To.Id == nodeId && e.ToPort == portName))
                {
                    return false;
                }
            }

            return true;
        }

        // ── Counts + seeds ──────────────────────────────────────────────────

        private int DrawCount(IntRange? spec, int fallback, string passKey, string tag)
        {
            if (spec == null)
            {
                return fallback;
            }

            int span = spec.Max - spec.Min; // >= 0 by IntRange.Validate
            if (span <= 0)
            {
                return spec.Min;
            }

            var rng = new JmoRng(SeedManager.DeriveChild(this._floorSeed, passKey, tag));
            return RangeRoll.Within(spec, rng.GetRndLong(span + 1));
        }

        // ── Result materialization ──────────────────────────────────────────

        public GraphGenerationResult ToResult(int attempts, bool succeeded)
        {
            var layout = new Dictionary<IGraphNode, ReservedRegion>(this._layout.Count);
            foreach (var (node, region) in this._layout)
            {
                layout[node] = region;
            }

            IFloorGraph? graph = this._g.HasSpineEndpoints ? this._g.ToFloorGraph() : null;
            return new GraphGenerationResult(graph, layout, attempts, this._warnings.ToList(), succeeded);
        }

        public void ReleaseAll()
        {
            foreach (var (_, region) in this._layout)
            {
                this._realizer.Release(in region);
            }
        }

        public static GraphGenerationResult Failure(int attempts, Violation cause)
            => new(
                null,
                new Dictionary<IGraphNode, ReservedRegion>(),
                attempts,
                new List<Violation> { cause },
                succeeded: false);

        private static Violation SpineInfeasible(string detail)
            => new(ViolationKind.SpineInfeasible, Severity.Fatal, detail);

        private static Violation PinUnsatisfiable(string detail)
            => new(ViolationKind.PinUnsatisfiable, Severity.Fatal, detail);

        private static string PortKey(StringName nodeId, StringName portName)
            => $"{nodeId}{Sep}{portName}";

        private static bool HasOpenTypeMatch(INodeTemplate t, StringName requiredType)
        {
            foreach (IGraphPort port in t.Ports)
            {
                if (TypeMatches(port.Type, requiredType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TypeMatches(StringName portType, StringName requiredType)
            => PortTypes.Matches(portType, requiredType);
    }
}
