namespace Jmodot.Implementation.ProcGen.Graph;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.Shared;
using Jmodot.Implementation.Shared;

/// <summary>
///     The constructive floor-graph generator (P3a.6, realizer-free since P3b.3, single-attempt
///     since P3b.5): turns an <see cref="ISkeletonConfig" /> + ONE floor seed into a deterministic,
///     gate-aware Source→Sink topology. A static utility —
///     every draw derives a distinct seeded sub-stream from the floor seed, so the whole algorithm
///     is RNG-free at the boundary and re-runnable byte-for-byte.
///     <para>
///         Stage 1 of the two-stage pipeline (design-se §1): this pass is pure topology — geometry
///         embedding is the holistic embedder's job (stage 2). <see cref="FloorPipeline"/> is the ONE
///         re-roll owner: it derives the per-attempt floor seed and decides retry-vs-fail-fast from
///         the returned violation kinds (<see cref="ViolationKind.PinUnsatisfiable" /> can never be
///         fixed by re-rolling; <see cref="ViolationKind.SpineInfeasible" /> can).
///     </para>
/// </summary>
internal static class GraphGenerator
{
    // ASCII unit separator — the same id-delimiter discipline PartialGraph / CandidateSlot use, so node
    // id segments can never be forged by id content.
    private const char Sep = (char)0x1F;

    // Default RNG factory: allocates a JmoRng per derived seed only when INVOKED (a lambda, not a
    // pre-constructed instance), so it never runs Godot's native StringName..cctor at type-load and
    // stays safe for pure-Logic call paths. An injected factory lets a future engine-free RNG swap in.
    private static readonly Func<int, IRng> DefaultRngFactory = seed => new JmoRng(seed);

    public static GraphGenerationResult GenerateSingle(
        ISkeletonConfig config, int floorSeed, Func<int, IRng>? rngFactory = null,
        Func<IFloorGraph, ILayoutAdvisor>? advisorFactory = null)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        config.Validate();

        var state = new GenState(config, floorSeed, rngFactory ?? DefaultRngFactory, advisorFactory);
        var outcome = state.BuildFloor(out var cause);
        return outcome == FloorOutcome.Ok
            ? state.ToResult(succeeded: true)
            : GenState.Failure(cause);
    }

    /// <summary>
    ///     Stable role-preference ordering: templates whose <see cref="INodeTemplate.Role" /> equals
    ///     <paramref name="preferred" /> come first, original pool order preserved within each group
    ///     (.NET <c>OrderBy</c> is a stable sort). Gives a pass's preferred role draw-order priority
    ///     (which cumulative-weight bucket a roll lands in) without disturbing determinism. Exposed
    ///     for direct unit testing of the sort.
    /// </summary>
    internal static IReadOnlyList<INodeTemplate> OrderByRolePreference(
        IReadOnlyList<INodeTemplate> templates, TemplateRole preferred)
        => templates.OrderBy(t => t.Role == preferred ? 0 : 1).ToList();

    /// <summary>
    ///     Whether a divergence/rejoin anchor pair at spine source-distances <paramref name="dx" /> (X)
    ///     and <paramref name="dy" /> (Y) is an eligible loop anchor. Lower bound: X must precede Y by at
    ///     least <paramref name="minSep" /> (<c>dx + minSep &lt;= dy</c>, implying X ≺ Y and non-degeneracy).
    ///     Upper bound: the separation must not exceed <paramref name="maxSep" /> so the route can SPAN the
    ///     gap and close on the grid — <paramref name="maxSep" /> &lt;= 0 disables the upper bound (unbounded).
    /// </summary>
    internal static bool IsAnchorPairEligible(int dx, int dy, int minSep, int maxSep)
        => dx + minSep <= dy && (maxSep <= 0 || dy - dx <= maxSep);

    /// <summary>
    ///     Adjusts a drawn route length so the resulting loop CYCLE has an EVEN edge count and can close
    ///     on the integer grid. A loop's cycle edges = route-side (<c>routeLen + 1</c>) + spine-side
    ///     (<paramref name="anchorDistance" />); each room step is an odd 3-cell move, so closure requires
    ///     that sum be even — i.e. <c>routeLen ≡ (1 + anchorDistance) mod 2</c>. An odd cycle is provably
    ///     un-embeddable (NoBinding every seed), so a route drawn with the wrong parity is nudged to the
    ///     nearest in-range value of the required parity (+1 then −1). Returns the drawn length unchanged
    ///     when <paramref name="min" />..<paramref name="max" /> holds no value of that parity.
    /// </summary>
    internal static int AdjustRouteLengthForClosure(int drawnLength, int anchorDistance, int min, int max)
    {
        int requiredParity = (1 + anchorDistance) % 2;
        if ((((drawnLength % 2) + 2) % 2) == requiredParity)
        {
            return drawnLength;
        }

        if (drawnLength + 1 <= max)
        {
            return drawnLength + 1;
        }

        if (drawnLength - 1 >= min)
        {
            return drawnLength - 1;
        }

        return drawnLength;
    }

    private enum FloorOutcome
    {
        Ok,
        SpineInfeasible,
        PinUnsatisfiable,
    }

    /// <summary>
    ///     One floor-build attempt's mutable working set: the builder, the tentatively-reserved
    ///     anchor ports, and the accumulated soft warnings. Re-created per attempt so a re-roll
    ///     starts from a clean graph.
    /// </summary>
    private sealed class GenState
    {
        private readonly ISkeletonConfig _config;
        private readonly int _floorSeed;
        private readonly Func<int, IRng> _rngFactory;

        // Optional geometry seam (Design B): when present, a freshly-laid loop/branch is trial-embedded
        // against the FROZEN spine before it is committed, so the generator only ships decorations that
        // actually close on the grid — turning whole-floor embed re-rolls into cheap local rejections.
        // Null = graph-only path (heuristics + parity nudge), fully standalone (two-stage decoupling).
        private readonly Func<IFloorGraph, ILayoutAdvisor>? _advisorFactory;
        private ILayoutAdvisor? _advisor;

        private readonly PartialGraph _g = new();
        private readonly List<Violation> _warnings = new();
        private readonly HashSet<string> _anchorReservedPorts = new();

        // Spine membership — loop routes may only anchor here (BlockExtractor's tree-path invariant:
        // the non-Loop skeleton must connect every pair of route anchors).
        private readonly HashSet<StringName> _spineNodeIds = new();

        // Spine nodes in placement order + the drawn pin map, kept for the pinned-neighbor pass
        // (it must re-address "the node at pin index i" after LaySpine returns).
        private readonly List<GraphNode> _spineNodes = new();
        private Dictionary<int, PinnedPlacement> _pinsByIndex = new();

        // Monotonic node-id ordinal — advances on every placement, guaranteeing globally-unique,
        // deterministic ids.
        private int _ordinal;

        // Monotonic loop-route ordinal — captured once per LayRouteBetween call and stamped on every edge
        // that route commits, so distinct routes get distinct ordinals and shared-anchor rings stay
        // disambiguated. One counter threads BOTH loop passes (guaranteed + opportunistic) — a per-pass
        // restart would collide (Loop, 0). Distinct from _ordinal, which counts per-placement attempts.
        private int _routeOrdinal;

        public GenState(
            ISkeletonConfig config, int floorSeed, Func<int, IRng> rngFactory,
            Func<IFloorGraph, ILayoutAdvisor>? advisorFactory = null)
        {
            this._config = config;
            this._floorSeed = floorSeed;
            this._rngFactory = rngFactory;
            this._advisorFactory = advisorFactory;
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

            // Spine committed → freeze it in the optional geometry advisor. Every decoration laid below is
            // now validated against the real grid before commit (Design B); a null factory keeps the
            // graph-only path. The spine snapshot shares node/edge instances with the graph that grows
            // beneath it, so the advisor's frozen poses stay valid through to the pipeline's BuildResult.
            this._advisor = this._advisorFactory?.Invoke(this._g.ToFloorGraph());

            // Required set-piece flanks attach before any decoration pass can consume the pinned
            // node's spare ports — loops and branches then plan around the committed neighbors.
            if (!this.AttachPinnedNeighbors(out cause))
            {
                return FloorOutcome.PinUnsatisfiable;
            }

            if (!this.LayGuaranteedLoops(out cause))
            {
                return FloorOutcome.SpineInfeasible; // a guaranteed loop could not be laid — re-roll
            }

            this.LayOpportunisticRoutes();
            this.LayBranches();

            int budgetMin = this._config.NodeBudget?.Min ?? 0;
            if (this._g.NodeCount < budgetMin)
            {
                this.Warn(new Violation(
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
            Dictionary<int, PinnedPlacement> forcedByIndex = this.ResolvePins(length);
            this._pinsByIndex = forcedByIndex;

            INodeTemplate? forcedFirst = forcedByIndex.GetValueOrDefault(0)?.AsNodeTemplate;
            (GraphNode? first, PortSlot? _) = this.PlaceNode(
                "spine", Array.Empty<PortSlot>(), requiredType: default, weights, constraints, forcedFirst);
            if (first == null)
            {
                cause = forcedFirst != null
                    ? PinUnsatisfiable("the Source pin's template could not be placed.")
                    : SpineInfeasible("no admissible template for the spine source.");
                return forcedFirst != null ? FloorOutcome.PinUnsatisfiable : FloorOutcome.SpineInfeasible;
            }

            this._g.SetSource(first);
            this._spineNodeIds.Add(first.Id);
            this._spineNodes.Add(first);
            GraphNode prev = first;
            int placed = 1;

            for (int i = 1; i < length; i++)
            {
                if (this._g.NodeCount >= this.BudgetMax)
                {
                    break; // live node-budget ceiling
                }

                // The joint (slot, template) draw enumerates every open port on prev, so the spine-exit
                // port on the current node is seed-varying (uniform when no weights are authored) rather
                // than always the first open port.
                List<PortSlot> anchors = this.OpenPortSlots(prev);
                if (anchors.Count == 0)
                {
                    break; // prev has no spare port to grow from — stop the spine here
                }

                // Interior nodes need an entry AND an exit port; only the tail may be a dead-end cap.
                int minPorts = i < length - 1 ? 2 : 1;
                INodeTemplate? forced = forcedByIndex.GetValueOrDefault(i)?.AsNodeTemplate;
                (GraphNode? node, PortSlot? slot) = this.PlaceNode(
                    "spine", anchors, requiredType: default, weights, constraints, forced, minPorts: minPorts);
                if (node == null || slot == null)
                {
                    cause = forced != null
                        ? PinUnsatisfiable($"the pin at spine index {i} could not be placed.")
                        : SpineInfeasible($"no admissible template for spine node {i}.");
                    return forced != null ? FloorOutcome.PinUnsatisfiable : FloorOutcome.SpineInfeasible;
                }

                IGraphPort? entry = this.SelectOpenPort(node, requiredType: slot.Port.Type);
                if (entry == null)
                {
                    cause = SpineInfeasible($"spine node {i} exposes no port compatible with its predecessor.");
                    return FloorOutcome.SpineInfeasible;
                }

                // A pin may gate its own exit: the edge leaving the pinned node (From = index i-1)
                // ships gated, blocking progression past the set-piece until its door opens.
                bool gated = forcedByIndex.GetValueOrDefault(i - 1)?.GateExitEdge == true;
                this._g.Connect(prev, slot.Port, node, entry, gated: gated, provenance: new EdgeProvenance(EdgeProvenanceKind.Spine, 0));
                this._spineNodeIds.Add(node.Id);
                this._spineNodes.Add(node);
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

            // Degradation stays bounded by the authored range: truncating WITHIN [Min, drawn] is
            // accepted, but a spine below Length.Min breaks the structural contract — re-roll.
            if (lengthSpec != null && placed < lengthSpec.Min)
            {
                cause = SpineInfeasible($"spine truncated at {placed} nodes, below Length.Min ({lengthSpec.Min}).");
                return FloorOutcome.SpineInfeasible;
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

            // Advisor mode: place each guaranteed loop on the first geometrically-fitting anchor pair,
            // retrying pairs WITHIN this attempt — a single ill-fitting pair becomes a cheap local
            // rejection instead of a whole-floor re-roll.
            if (this._advisor != null)
            {
                return this.LayGuaranteedLoopsValidated(spec, guaranteed, minSep, out cause);
            }

            List<AnchorPair> pairs = this.PickAnchorPairs("guaranteed", guaranteed, minSep, spec.MaxAnchorSeparation, spec.EffectiveAttachmentWeights);

            foreach (AnchorPair pair in pairs)
            {
                if (!this.LayRouteBetween(pair, spec, "guaranteed"))
                {
                    cause = new Violation(
                        ViolationKind.SpineInfeasible, Severity.Fatal,
                        "A guaranteed alternate route could not be laid.");
                    return false;
                }
            }

            if (pairs.Count < guaranteed)
            {
                // Fewer eligible anchor pairs than requested — surfaced as a soft warning (the floor is
                // still a valid connected topology; the backbone-feasibility Validate gate guards the
                // authored config, but a sampled short or spare-port-poor spine can still under-fill).
                this.Warn(new Violation(
                    ViolationKind.AlternateRoutesUnfilled, Severity.Warning,
                    $"Laid {pairs.Count} guaranteed loops; {guaranteed} requested."));
            }

            return true;
        }

        // Advisor mode: lay each guaranteed loop on the first candidate anchor pair whose route closes
        // on the FROZEN spine, retrying pairs within this attempt. Only a loop that NO eligible pair can
        // satisfy forces a re-roll (genuine frozen-spine infeasibility).
        private bool LayGuaranteedLoopsValidated(AlternateRouteSpec spec, int guaranteed, int minSep, out Violation cause)
        {
            cause = default;
            for (int loop = 0; loop < guaranteed; loop++)
            {
                if (!this.TryLayValidatedLoop(spec, minSep, "guaranteed"))
                {
                    cause = new Violation(
                        ViolationKind.SpineInfeasible, Severity.Fatal,
                        "A guaranteed alternate route could not be laid on the frozen spine.");
                    return false;
                }
            }

            return true;
        }

        // Tries eligible anchor pairs GEOMETRY-FIRST (smallest real grid gap first — the pairs a short
        // route can actually span, including the spine-folds-back-near-itself case the graph-distance
        // proxy misses) until one yields a grid-closable route. Each failed pair is fully rolled back by
        // LayRouteBetween and releases its reserved ports, so the retry is side-effect-free.
        private bool TryLayValidatedLoop(AlternateRouteSpec spec, int minSep, string passKey)
        {
            if (this._advisor == null || !this._g.TryGetMetrics(out IGraphMetrics metrics))
            {
                return false;
            }

            List<(GraphNode X, GraphNode Y)> eligible = this.EnumerateEligiblePairs(metrics, minSep, spec.MaxAnchorSeparation)
                .OrderBy(p => this._advisor.GridStepDistance(p.X.Id, p.Y.Id) ?? int.MaxValue)
                .ToList();

            foreach ((GraphNode X, GraphNode Y) in eligible)
            {
                IGraphPort? xPort = this.SelectAnchorPort(X);
                IGraphPort? yPort = this.SelectAnchorPort(Y);
                if (xPort == null || yPort == null)
                {
                    continue;
                }

                this._anchorReservedPorts.Add(PortKey(X.Id, xPort.Name));
                this._anchorReservedPorts.Add(PortKey(Y.Id, yPort.Name));

                if (this.LayRouteBetween(new AnchorPair(X, xPort, Y, yPort), spec, passKey))
                {
                    return true;
                }

                this._anchorReservedPorts.Remove(PortKey(X.Id, xPort.Name));
                this._anchorReservedPorts.Remove(PortKey(Y.Id, yPort.Name));
            }

            return false;
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
        private List<AnchorPair> PickAnchorPairs(string passKey, int count, int minSep, int maxSep, IReadOnlyList<EndpointWeight> weights)
        {
            var result = new List<AnchorPair>();
            if (!this._g.TryGetMetrics(out IGraphMetrics metrics))
            {
                return result; // no spine ⇒ no anchors (defensive; spine is laid first)
            }

            for (int pick = 0; pick < count; pick++)
            {
                List<(GraphNode X, GraphNode Y)> eligible = this.EnumerateEligiblePairs(metrics, minSep, maxSep);
                if (eligible.Count == 0)
                {
                    break;
                }

                var choices = new List<((GraphNode X, GraphNode Y) Pair, long Weight)>(eligible.Count);
                foreach ((GraphNode X, GraphNode Y) p in eligible)
                {
                    choices.Add((p, this.EndpointWeightProduct(p, weights)));
                }

                var rng = this._rngFactory(SeedManager.DeriveChild(this._floorSeed, passKey, "anchors", pick.ToString()));
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

        private List<(GraphNode X, GraphNode Y)> EnumerateEligiblePairs(IGraphMetrics metrics, int minSep, int maxSep)
        {
            var nodes = this._g.Nodes;
            var withSpare = nodes
                .Where(n => this._spineNodeIds.Contains(n.Id) && this.SelectAnchorPort(n) != null)
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
                    if (IsAnchorPairEligible(dx, dy, minSep, maxSep)) // X ≺ Y, within [minSep, maxSep] separation
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
        ///     Lays a route of routing nodes from <c>pair.X</c> and closes onto <c>pair.Y</c>. Template
        ///     selection is deterministic per slot, so a route either resolves every slot or fails
        ///     outright (no admissible template — returns false; guaranteed routes re-roll the floor,
        ///     opportunistic routes soft-skip). Commits AddNode + Connect only once the full chain
        ///     resolves; closure onto Y is topological (Y's reserved spare port).
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

            // Closure-parity: the loop cycle (route-side + spine-side edges) must be EVEN to close on the
            // grid. Nudge the drawn length to the parity that the anchor spine-distance demands — an odd
            // cycle is provably un-embeddable, so this is the difference between a closable loop and a
            // guaranteed re-roll. Spine-distance is exact for the first loop (spine-only metrics); later
            // loops approximate via the live source-distance, which is acceptable for a parity nudge.
            if (spec.Length != null && this._g.TryGetMetrics(out IGraphMetrics metrics))
            {
                int anchorDistance = metrics.DistanceFromSource(pair.Y) - metrics.DistanceFromSource(pair.X);
                routeLen = AdjustRouteLengthForClosure(routeLen, anchorDistance, spec.Length.Min, spec.Length.Max);
            }

            var prov = new List<(StringName Id, INodeTemplate Template)>();
            StringName entryType = pair.XPort.Type;

            while (prov.Count < routeLen)
            {
                if (this._g.NodeCount + prov.Count >= this.BudgetMax)
                {
                    break; // live ceiling — close the route early with what is laid so far
                }

                int ord = this._ordinal++;
                var id = new StringName($"{passKey}{Sep}{ord}");

                // minPorts 2: a route node must pass through (entry + exit); a 1-port dead-end here
                // would fail the whole route at commit time.
                (INodeTemplate? template, PortSlot? _) = this.SelectTemplate(
                    id, Array.Empty<PortSlot>(), entryType, Array.Empty<SlotWeight>(), Array.Empty<SlotConstraint>(),
                    passKey, ord, forced: null, pref: TemplateRole.Connector, minPorts: 2);
                if (template == null)
                {
                    return false; // no admissible routing template — deterministic, retry cannot help
                }

                prov.Add((id, template));
            }

            if (prov.Count == 0)
            {
                return false; // budget left no room for even one routing node
            }

            PartialGraph.GraphCheckpoint cp = this._g.Checkpoint();
            if (!this.CommitRoute(pair, prov, routeOrdinal))
            {
                this._g.RollbackTo(cp);
                return false;
            }

            // Geometry gate (advisor mode): commit the route only if it actually closes on the grid.
            // A rejected route is rolled back so it never ships as an unembeddable loop — which would
            // otherwise force a whole-floor re-roll at the embed stage. No advisor ⇒ no gate (unchanged).
            if (this._advisor != null && !this._advisor.TryCommitSubgraph(this._g.ToFloorGraph()))
            {
                this._g.RollbackTo(cp);
                return false;
            }

            return true;
        }

        private bool CommitRoute(AnchorPair pair, List<(StringName Id, INodeTemplate Template)> prov, int routeOrdinal)
        {
            // Resolve the FULL entry/exit port chain BEFORE mutating the graph. The previous form
            // committed every node up front and then walked binding ports, so a mid-walk bail (a route
            // node whose entry cannot match the predecessor's exit type) left orphan nodes + a dangling
            // Loop-stamped chain behind — and for opportunistic routes that failure is soft-skipped, so
            // the malformed topology shipped in a "successful" graph. Route nodes are fresh, so port
            // occupancy is local to the route; track claimed ports per node without touching the graph,
            // mirroring SelectOpenPort's template-order, wildcard-aware pick so committed routes stay
            // byte-identical to the pre-staging path.
            var plan = new List<(IGraphPort Entry, IGraphPort Exit)>(prov.Count);
            StringName prevPortType = pair.XPort.Type;
            foreach (var step in prov)
            {
                var claimed = new HashSet<StringName>();
                IGraphPort? entry = FirstSparePort(step.Template, prevPortType, claimed);
                if (entry == null)
                {
                    return false; // no compatible entry port — commit nothing
                }

                claimed.Add(entry.Name);
                IGraphPort? exit = FirstSparePort(step.Template, requiredType: default, claimed);
                if (exit == null)
                {
                    return false; // no spare exit to continue / close — commit nothing
                }

                plan.Add((entry, exit));
                prevPortType = exit.Type;
            }

            // Full chain resolved (closure onto Y is topological). Mutate atomically: add all route
            // nodes, then wire X → n1 → … → Y in the same order the pre-staging path did.
            var nodes = new List<GraphNode>(prov.Count);
            foreach (var step in prov)
            {
                nodes.Add(this._g.AddNode(step.Id, step.Template));
            }

            GraphNode prevNode = pair.X;
            IGraphPort prevPort = pair.XPort;
            for (int i = 0; i < nodes.Count; i++)
            {
                this._g.Connect(prevNode, prevPort, nodes[i], plan[i].Entry, provenance: new EdgeProvenance(EdgeProvenanceKind.Loop, routeOrdinal));
                prevNode = nodes[i];
                prevPort = plan[i].Exit;
            }

            this._g.Connect(prevNode, prevPort, pair.Y, pair.YPort, provenance: new EdgeProvenance(EdgeProvenanceKind.Loop, routeOrdinal)); // topological closure onto Y
            return true;
        }

        // Template-order, wildcard-aware spare-port pick for a FRESH route node: a port is spare when
        // not yet claimed earlier in this route's plan (a fresh node has no graph edges). Mirrors
        // SelectOpenPort so a staged commit selects the identical ports the live walk would have.
        private static IGraphPort? FirstSparePort(INodeTemplate template, StringName requiredType, HashSet<StringName> claimed)
        {
            foreach (IGraphPort port in template.Ports)
            {
                if (!claimed.Contains(port.Name) && TypeMatches(port.Type, requiredType))
                {
                    return port;
                }
            }

            return null;
        }

        private Dictionary<int, PinnedPlacement> ResolvePins(int length)
        {
            var map = new Dictionary<int, PinnedPlacement>();
            SpineSpec? spine = this._config.Spine;
            if (spine?.SourcePin?.AsNodeTemplate != null)
            {
                map[0] = spine.SourcePin;
            }

            if (length > 0 && spine?.SinkPin?.AsNodeTemplate != null)
            {
                map[length - 1] = spine.SinkPin;
            }

            foreach (PinnedPlacement pin in this._config.Pins)
            {
                if (pin?.Anchor == null)
                {
                    continue;
                }

                int idx = pin.Anchor.ResolveSpineIndex(this._config, length);
                if (idx >= 0 && idx < length && pin.AsNodeTemplate != null)
                {
                    map[idx] = pin; // interior pins win over the endpoint defaults at a shared index
                }
            }

            return map;
        }

        // ── Pinned neighbors (required set-piece flanks) ────────────────────

        /// <summary>
        ///     Attaches every pin's <see cref="PinnedPlacement.RequiredNeighbors" /> directly to its
        ///     pinned spine node as dead-end pockets (<see cref="EdgeProvenanceKind.PinnedNeighbor" />).
        ///     Runs immediately after the spine commits, so the required rooms claim the pinned node's
        ///     spare ports before any decoration pass can. Any failure — port exhaustion, budget
        ///     ceiling, or a geometry rejection in advisor mode — is PinUnsatisfiable: required
        ///     content, consistent with every other pin path (a mid-spine forced-placement failure is
        ///     already PinUnsatisfiable even in advisor mode).
        /// </summary>
        private bool AttachPinnedNeighbors(out Violation cause)
        {
            cause = default;
            foreach (int idx in this._pinsByIndex.Keys.OrderBy(k => k))
            {
                PinnedPlacement pin = this._pinsByIndex[idx];
                foreach (INodeTemplate neighbor in pin.RequiredNeighborTemplates)
                {
                    if (this._g.NodeCount >= this.BudgetMax)
                    {
                        cause = PinUnsatisfiable(
                            $"the pin at spine index {idx} requires neighbor '{neighbor.TemplateId}' but NodeBudget.Max ({this.BudgetMax}) is already reached.");
                        return false;
                    }

                    GraphNode host = this._spineNodes[idx];
                    IGraphPort? exit = this.SelectOpenPort(host, requiredType: default);
                    if (exit == null)
                    {
                        cause = PinUnsatisfiable(
                            $"the pin at spine index {idx} has no spare port for required neighbor '{neighbor.TemplateId}'.");
                        return false;
                    }

                    PartialGraph.GraphCheckpoint cp = this._g.Checkpoint();
                    (GraphNode? child, PortSlot? _) = this.PlaceNode(
                        "pinned", new List<PortSlot> { new PortSlot(host, exit) }, requiredType: default,
                        Array.Empty<SlotWeight>(), Array.Empty<SlotConstraint>(), forced: neighbor);
                    if (child == null)
                    {
                        cause = PinUnsatisfiable(
                            $"required neighbor '{neighbor.TemplateId}' at spine index {idx} exposes no port compatible with its host.");
                        return false;
                    }

                    IGraphPort? entry = this.SelectOpenPort(child, exit.Type);
                    if (entry == null)
                    {
                        cause = PinUnsatisfiable(
                            $"required neighbor '{neighbor.TemplateId}' at spine index {idx} exposes no entry port.");
                        return false;
                    }

                    this._g.Connect(host, exit, child, entry, provenance: new EdgeProvenance(EdgeProvenanceKind.PinnedNeighbor, idx));

                    if (this._advisor != null && !this._advisor.TryCommitSubgraph(this._g.ToFloorGraph()))
                    {
                        this._g.RollbackTo(cp);
                        cause = PinUnsatisfiable(
                            $"required neighbor '{neighbor.TemplateId}' at spine index {idx} cannot be embedded beside its host.");
                        return false;
                    }
                }
            }

            return true;
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

            List<AnchorPair> pairs = this.PickAnchorPairs("opportunistic", opportunistic, spec.MinAnchorSeparation, spec.MaxAnchorSeparation, spec.EffectiveAttachmentWeights);
            int laid = 0;
            foreach (AnchorPair pair in pairs)
            {
                if (this._g.NodeCount >= this.BudgetMax)
                {
                    break;
                }

                if (this.LayRouteBetween(pair, spec, "opportunistic"))
                {
                    laid++;
                }

                // else: soft-skip — an opportunistic route that cannot close is simply dropped.
            }

            int requestedMin = spec.OpportunisticCount?.Min ?? 0;
            if (laid < requestedMin)
            {
                this.Warn(new Violation(
                    ViolationKind.AlternateRoutesUnfilled, Severity.Warning,
                    $"Laid {laid} opportunistic routes; {requestedMin} requested."));
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

            // A branch with fanout 0 grows no children — a degenerate "branch" that silently breaks
            // Count.Min's promise of at least one branch. FanOut.Min defaults to 0 (an unset IntRange),
            // so this single per-floor draw can roll 0 and zero EVERY branch on the floor. Floor the
            // branching factor at 1 whenever branching is requested (guard at the consumption site, per
            // the inspector-driven-range rule), so no profile can void Count.Min via an orthogonal knob.
            int fanout = Math.Max(1, this.DrawCount(spec.FanOut, fallback: 1, "branch", "fanout"));
            var weights = spec.EffectiveWeights;
            var constraints = spec.EffectiveConstraints;

            // Advisor mode: branches get the same anchor RETRY the geometry-aware loops have — a graph-order
            // anchor the loops boxed in is excluded and the next distinct anchor is tried, instead of the
            // stateless picker re-selecting the same doomed node every iteration.
            if (this._advisor != null)
            {
                this.LayBranchesValidated(spec, count, depth, fanout, weights, constraints);
                return;
            }

            int grown = 0;
            for (int b = 0; b < count; b++)
            {
                // The joint (slot, template) draw now selects the branch anchor from every open
                // (node, port) pair, so the anchor node is seed-varying instead of always spine⟨0⟩.
                IReadOnlyList<PortSlot> anchors = this.PickBranchAnchors();
                if (anchors.Count == 0)
                {
                    break; // no node has a spare port — stop branching
                }

                int before = this._g.NodeCount;
                this.GrowBranch(anchors, depth, fanout, weights, constraints, b);
                if (this._g.NodeCount > before)
                {
                    grown++;
                }
            }

            int branchMin = spec.Count?.Min ?? 0;
            if (grown < branchMin)
            {
                this.Warn(new Violation(
                    ViolationKind.BranchesUnfilled, Severity.Warning,
                    $"Grew {grown} branches; BranchSpec.Count.Min is {branchMin}."));
            }
        }

        // Advisor-mode branch placement: try candidate anchors until one grows >=1 child, EXCLUDING any
        // anchor whose growth was rolled back (its spare port opens into loop-occupied grid). The
        // authoritative free-space gate is GrowBranch's per-child TryCommitSubgraph; this supplies only
        // the retry-across-distinct-anchors the stateless PickBranchAnchor lacked. Candidate order is
        // graph-insertion (deterministic); a good anchor with spare ports left can host later branches too.
        private void LayBranchesValidated(
            BranchSpec spec, int count, int depth, int fanout,
            IReadOnlyList<SlotWeight> weights, IReadOnlyList<SlotConstraint> constraints)
        {
            var candidates = this._g.Nodes
                .Where(n => this.SelectOpenPort(n, requiredType: default) != null)
                .ToList();
            var doomed = new HashSet<StringName>();

            int grown = 0;
            for (int b = 0; b < count; b++)
            {
                bool placed = false;
                foreach (GraphNode anchor in candidates)
                {
                    if (doomed.Contains(anchor.Id) || this.SelectOpenPort(anchor, requiredType: default) == null)
                    {
                        continue;
                    }

                    int before = this._g.NodeCount;
                    this.GrowBranch(this.OpenPortSlots(anchor), depth, fanout, weights, constraints, b);
                    if (this._g.NodeCount > before)
                    {
                        grown++;
                        placed = true;
                        break;
                    }

                    doomed.Add(anchor.Id); // grew nothing on the frozen grid — never useful again this floor
                }

                if (!placed)
                {
                    break; // no remaining candidate anchor can host a branch on this floor
                }
            }

            int branchMin = spec.Count?.Min ?? 0;
            if (grown < branchMin)
            {
                this.Warn(new Violation(
                    ViolationKind.BranchesUnfilled, Severity.Warning,
                    $"Grew {grown} branches; BranchSpec.Count.Min is {branchMin}."));
            }
        }

        // rootOrdinal = the b-th branch growth (the LayBranches loop index); the whole tree under this
        // root shares it, even when a branch roots on a node an earlier branch created. anchors = the
        // candidate open (node, port) slots; the joint draw selects one per child.
        private void GrowBranch(
            IReadOnlyList<PortSlot> anchors, int depth, int fanout,
            IReadOnlyList<SlotWeight> weights, IReadOnlyList<SlotConstraint> constraints, int rootOrdinal)
        {
            if (depth <= 0)
            {
                return;
            }

            List<PortSlot> openSlots = anchors.Count > 0 ? anchors.ToList() : this.PickBranchAnchors();
            for (int f = 0; f < fanout; f++)
            {
                if (this._g.NodeCount >= this.BudgetMax)
                {
                    return; // live ceiling
                }

                // A prior fanout sibling consumed a port; re-derive so it is never re-drawn. The
                // caller's anchors seed the first iteration (identical to a fresh enumerate then).
                if (f > 0)
                {
                    openSlots = this.PickBranchAnchors();
                }

                if (openSlots.Count == 0)
                {
                    return; // no node has a spare port — stop this branch
                }

                PartialGraph.GraphCheckpoint cp = this._g.Checkpoint();

                // Free-growth passes (spine + branch) prefer Body — branches dead-end into pocket
                // rooms; only routing passes prefer Connector (design-se §2). The joint draw selects
                // the (anchor slot, template) pair, so the branch's attachment node+port is seed-varying.
                (GraphNode? child, PortSlot? slot) = this.PlaceNode(
                    "branch", openSlots, requiredType: default, weights, constraints, forced: null);
                if (child == null || slot == null)
                {
                    return; // no admissible (slot, template) — soft-skip the rest of this branch
                }

                IGraphPort? entry = this.SelectOpenPort(child, slot.Port.Type);
                if (entry == null)
                {
                    return;
                }

                this._g.Connect((GraphNode)slot.Node, slot.Port, child, entry, provenance: new EdgeProvenance(EdgeProvenanceKind.Branch, rootOrdinal));

                // Geometry gate (advisor mode): a branch child that cannot be placed on the grid is rolled
                // back and the next fanout slot is tried, rather than shipping an unembeddable branch. No
                // advisor ⇒ no gate (unchanged greedy growth).
                if (this._advisor != null && !this._advisor.TryCommitSubgraph(this._g.ToFloorGraph()))
                {
                    this._g.RollbackTo(cp);
                    continue;
                }

                this.GrowBranch(this.OpenPortSlots(child), depth - 1, fanout, weights, constraints, rootOrdinal);
            }
        }

        private List<PortSlot> PickBranchAnchors()
        {
            var anchors = new List<PortSlot>();
            foreach (GraphNode node in this._g.Nodes)
            {
                anchors.AddRange(this.OpenPortSlots(node, requiredType: default));
            }

            return anchors;
        }

        // ── Placement primitives ────────────────────────────────────────────

        /// <summary>
        ///     Commits a new node: resolves a (template, slot) pair then AddNode. Returns the chosen slot
        ///     so the caller connects the edge through the port the joint draw selected. Null template
        ///     when no (slot, template) pair is constraint-admissible. Used commit-as-you-go for the spine
        ///     (a mid-spine failure re-rolls the floor).
        /// </summary>
        private (GraphNode? Node, PortSlot? Slot) PlaceNode(
            string passKey, IReadOnlyList<PortSlot> anchors, StringName requiredType,
            IReadOnlyList<SlotWeight> weights, IReadOnlyList<SlotConstraint> constraints,
            INodeTemplate? forced = null, TemplateRole pref = TemplateRole.Body, int minPorts = 1)
        {
            int ord = this._ordinal++;
            var nodeId = new StringName($"{passKey}{Sep}{ord}");

            (INodeTemplate? template, PortSlot? slot) = this.SelectTemplate(
                nodeId, anchors, requiredType, weights, constraints, passKey, ord, forced, pref, minPorts);
            if (template == null)
            {
                return (null, null);
            }

            return (this._g.AddNode(nodeId, template), slot);
        }

        /// <summary>
        ///     Resolves a (template, slot) pair for <paramref name="nodeId" />. A forced template
        ///     (pin) overrides free selection, pairing with the first compatible anchor slot (or null
        ///     when no anchor is given). Otherwise the joint <see cref="WeightedDraw" /> picks one
        ///     <c>(slot, template)</c> pair over the cross-product. Returns <c>(null, null)</c> when no
        ///     pair is admissible. Does NOT touch the graph — caller commits.
        /// </summary>
        private (INodeTemplate?, PortSlot?) SelectTemplate(
            StringName nodeId, IReadOnlyList<PortSlot> anchors, StringName requiredType,
            IReadOnlyList<SlotWeight> weights, IReadOnlyList<SlotConstraint> constraints,
            string passKey, int ordinal,
            INodeTemplate? forced = null, TemplateRole pref = TemplateRole.Body, int minPorts = 1)
        {
            if (forced != null)
            {
                // A pin overrides free selection (and the constraint + minPorts filters — it is
                // authored intent); it still requires a port compatible with its predecessor. With a
                // placement context the forced template must match some anchor slot; without one (the
                // spine-source pin) it must match the caller's requiredType.
                if (anchors.Count > 0)
                {
                    PortSlot? slot = this.FirstCompatibleSlot(anchors, forced);
                    return slot == null ? (null, null) : (forced, slot);
                }

                return HasOpenTypeMatch(forced, requiredType)
                    ? (forced, (PortSlot?)null)
                    : (null, null);
            }

            // Candidates are filtered by minPorts + constraints; per-slot type compatibility and the
            // per-(slot, template) constraints are applied inside the joint draw, because a template
            // admissible for one slot need not be for another. Empty anchors (spine source / route
            // node) keep the caller's requiredType filter.
            List<INodeTemplate> candidates = OrderByRolePreference(
                this._config.TemplatePool
                    .Where(t => t.Ports.Count >= minPorts)
                    .Where(t => anchors.Count == 0 ? HasOpenTypeMatch(t, requiredType) : true)
                    .ToList(),
                pref).ToList();

            if (candidates.Count == 0)
            {
                return (null, null);
            }

            return this.WeightedDraw(candidates, anchors, passKey, nodeId, ordinal, weights, constraints);
        }

        /// <summary>
        ///     The joint weighted draw over the (slot, template) cross-product: each open anchor slot ×
        ///     each type-compatible, constraint-admissible candidate scores its placement weight, and one
        ///     pair is rolled. With zero authored weights every pair scores 1, so the draw is uniform
        ///     over all open ports (the default behavior). The seed label is content-addressed per node,
        ///     so widening the table does not shift any other draw stream.
        /// </summary>
        private (INodeTemplate?, PortSlot?) WeightedDraw(
            List<INodeTemplate> candidates, IReadOnlyList<PortSlot> anchors,
            string passKey, StringName nodeId, int ordinal,
            IReadOnlyList<SlotWeight> weights, IReadOnlyList<SlotConstraint> constraints)
        {
            bool hasMetrics = this._g.TryGetMetrics(out _);
            var choices = new List<((INodeTemplate Template, PortSlot? Slot) Item, long Weight)>();

            if (anchors.Count == 0)
            {
                // No placement context (spine source / route node): template-only, neutral weight,
                // constraints skipped — matching the pre-joint-draw single-anchor path.
                foreach (INodeTemplate t in candidates)
                {
                    choices.Add(((t, (PortSlot?)null), 1L));
                }
            }
            else
            {
                foreach (PortSlot slot in anchors)
                {
                    foreach (INodeTemplate t in candidates)
                    {
                        if (!HasOpenTypeMatch(t, slot.Port.Type) || !this.PassesConstraints(t, slot, constraints))
                        {
                            continue;
                        }

                        choices.Add(((t, slot), this.PlacementWeightProduct(slot, t, weights, hasMetrics)));
                    }
                }
            }

            if (choices.Count == 0)
            {
                return (null, null);
            }

            var rng = this._rngFactory(SeedManager.DeriveChild(
                this._floorSeed, passKey, "pick", nodeId.ToString(), ordinal.ToString()));
            long total = WeightedPick.TotalWeight(choices);
            long roll = rng.GetRndLong(total);
            return WeightedPick.Pick(choices, roll);
        }

        private long PlacementWeightProduct(PortSlot? anchor, INodeTemplate t, IReadOnlyList<SlotWeight> weights, bool hasMetrics)
        {
            if (anchor == null)
            {
                return 1; // no placement context (spine source / route node) — neutral
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

        private bool PassesConstraints(INodeTemplate t, PortSlot anchor, IReadOnlyList<SlotConstraint> constraints)
        {
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

        /// <summary>
        ///     Every open, type-compatible port on <paramref name="node" /> as a <see cref="PortSlot" />,
        ///     in template order. The candidate universe for the joint (slot, template) draw.
        /// </summary>
        private List<PortSlot> OpenPortSlots(GraphNode node, StringName requiredType = default)
        {
            var slots = new List<PortSlot>();
            foreach (IGraphPort port in node.Template.Ports)
            {
                if (this.IsPortOpen(node.Id, port.Name) && TypeMatches(port.Type, requiredType))
                {
                    slots.Add(new PortSlot(node, port));
                }
            }

            return slots;
        }

        private PortSlot? FirstCompatibleSlot(IReadOnlyList<PortSlot> anchors, INodeTemplate template)
        {
            foreach (PortSlot slot in anchors)
            {
                if (HasOpenTypeMatch(template, slot.Port.Type))
                {
                    return slot;
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

        // Records a non-fatal advisory and mirrors it to the log, so silent soft-degradations
        // (under-filled branches / routes / budget) surface in the post-run godot.log for debugging —
        // not only in the returned result's warning list. Warning level never fails a test (only Error
        // does), so this is safe on every generator path (all of which run under the Godot runtime).
        private void Warn(Violation v)
        {
            this._warnings.Add(v);
            JmoLogger.Warning(this, $"[ProcGen] {v.Reason}: {v.Detail}");
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

            var rng = this._rngFactory(SeedManager.DeriveChild(this._floorSeed, passKey, tag));
            return RangeRoll.Within(spec, rng.GetRndLong(span + 1));
        }

        // ── Result materialization ──────────────────────────────────────────

        public GraphGenerationResult ToResult(bool succeeded)
        {
            IFloorGraph? graph = this._g.HasSpineEndpoints ? this._g.ToFloorGraph() : null;
            return new GraphGenerationResult(graph, this._warnings.ToList(), succeeded);
        }

        public static GraphGenerationResult Failure(Violation cause)
            => new(
                null,
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
