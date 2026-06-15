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
///     gate-aware Source→Sink topology. A static utility (matching <c>PointCloudGenerator</c>) —
///     every draw derives a distinct seeded sub-stream from the floor seed, so the whole algorithm
///     is RNG-free at the boundary and re-runnable byte-for-byte.
///     <para>
///         Stage 1 of the two-stage pipeline (design-se §1): this pass is pure topology — geometry
///         embedding is the holistic embedder's job (stage 2). <c>FloorPipeline</c> is the ONE
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

            if (!this.LayGuaranteedLoops(out cause))
            {
                return FloorOutcome.SpineInfeasible; // a guaranteed loop could not be laid — re-roll
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
            GraphNode? first = this.PlaceNode("spine", requiredType: default, anchor: null, weights, constraints, forcedFirst);
            if (first == null)
            {
                cause = forcedFirst != null
                    ? PinUnsatisfiable("the Source pin's template could not be placed.")
                    : SpineInfeasible("no admissible template for the spine source.");
                return forcedFirst != null ? FloorOutcome.PinUnsatisfiable : FloorOutcome.SpineInfeasible;
            }

            this._g.SetSource(first);
            this._spineNodeIds.Add(first.Id);
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

                // Interior nodes need an entry AND an exit port; only the tail may be a dead-end cap.
                int minPorts = i < length - 1 ? 2 : 1;
                INodeTemplate? forced = forcedByIndex.GetValueOrDefault(i);
                GraphNode? node = this.PlaceNode(
                    "spine", requiredType: exit.Type,
                    anchor: new PortSlot(prev, exit), weights, constraints, forced, minPorts: minPorts);
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
                this._spineNodeIds.Add(node.Id);
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
                this._warnings.Add(new Violation(
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
                INodeTemplate? template = this.SelectTemplate(
                    id, entryType, anchor: null, Array.Empty<SlotWeight>(), Array.Empty<SlotConstraint>(), passKey, ord, forced: null, pref: TemplateRole.Connector, minPorts: 2);
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
                this._warnings.Add(new Violation(
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
            int fanout = this.DrawCount(spec.FanOut, fallback: 1, "branch", "fanout");
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
                GraphNode? anchor = this.PickBranchAnchor();
                if (anchor == null)
                {
                    break; // no node has a spare port — stop branching
                }

                int before = this._g.NodeCount;
                this.GrowBranch(anchor, depth, fanout, weights, constraints, b);
                if (this._g.NodeCount > before)
                {
                    grown++;
                }
            }

            int branchMin = spec.Count?.Min ?? 0;
            if (grown < branchMin)
            {
                this._warnings.Add(new Violation(
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
                    this.GrowBranch(anchor, depth, fanout, weights, constraints, b);
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
                this._warnings.Add(new Violation(
                    ViolationKind.BranchesUnfilled, Severity.Warning,
                    $"Grew {grown} branches; BranchSpec.Count.Min is {branchMin}."));
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

                PartialGraph.GraphCheckpoint cp = this._g.Checkpoint();

                // Free-growth passes (spine + branch) prefer Body — branches dead-end into pocket
                // rooms; only routing passes prefer Connector (design-se §2).
                GraphNode? child = this.PlaceNode(
                    "branch", requiredType: exit.Type,
                    anchor: new PortSlot(parent, exit), weights, constraints, forced: null);
                if (child == null)
                {
                    return; // no admissible template — soft-skip the rest of this branch
                }

                IGraphPort? entry = this.SelectOpenPort(child, exit.Type);
                if (entry == null)
                {
                    return;
                }

                this._g.Connect(parent, exit, child, entry, provenance: new EdgeProvenance(EdgeProvenanceKind.Branch, rootOrdinal));

                // Geometry gate (advisor mode): a branch child that cannot be placed on the grid is rolled
                // back and the next fanout slot is tried, rather than shipping an unembeddable branch. No
                // advisor ⇒ no gate (unchanged greedy growth).
                if (this._advisor != null && !this._advisor.TryCommitSubgraph(this._g.ToFloorGraph()))
                {
                    this._g.RollbackTo(cp);
                    continue;
                }

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
        ///     Commits a new node: resolves a template then AddNode. Returns null when no template is
        ///     constraint-admissible. Used commit-as-you-go for the spine (a mid-spine failure
        ///     re-rolls the floor).
        /// </summary>
        private GraphNode? PlaceNode(
            string passKey, StringName requiredType,
            PortSlot? anchor, IReadOnlyList<SlotWeight> weights, IReadOnlyList<SlotConstraint> constraints,
            INodeTemplate? forced = null, TemplateRole pref = TemplateRole.Body, int minPorts = 1)
        {
            int ord = this._ordinal++;
            var nodeId = new StringName($"{passKey}{Sep}{ord}");

            INodeTemplate? template = this.SelectTemplate(nodeId, requiredType, anchor, weights, constraints, passKey, ord, forced, pref, minPorts);
            if (template == null)
            {
                return null;
            }

            return this._g.AddNode(nodeId, template);
        }

        /// <summary>
        ///     Resolves a template for <paramref name="nodeId" />: hard-filters the pool by port-type
        ///     compatibility + constraints, then weighted-draws a candidate. Returns null on an empty
        ///     candidate set. Does NOT touch the graph — caller commits.
        /// </summary>
        private INodeTemplate? SelectTemplate(
            StringName nodeId, StringName requiredType, PortSlot? anchor,
            IReadOnlyList<SlotWeight> weights, IReadOnlyList<SlotConstraint> constraints,
            string passKey, int ordinal,
            INodeTemplate? forced = null, TemplateRole pref = TemplateRole.Body, int minPorts = 1)
        {
            List<INodeTemplate> candidates;
            if (forced != null)
            {
                // A pin overrides free selection (and the constraint + minPorts filters — it is
                // authored intent); it still requires a port compatible with its predecessor.
                candidates = HasOpenTypeMatch(forced, requiredType)
                    ? new List<INodeTemplate> { forced }
                    : new List<INodeTemplate>();
            }
            else
            {
                candidates = OrderByRolePreference(
                    this._config.TemplatePool
                        .Where(t => t.Ports.Count >= minPorts)
                        .Where(t => HasOpenTypeMatch(t, requiredType))
                        .Where(t => this.PassesConstraints(t, anchor, constraints))
                        .ToList(),
                    pref).ToList();
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return this.WeightedDraw(candidates, passKey, nodeId, ordinal, anchor, weights);
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

        private bool PassesConstraints(INodeTemplate t, PortSlot? anchor, IReadOnlyList<SlotConstraint> constraints)
        {
            if (anchor == null)
            {
                return true; // no placement context (spine source / route node)
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
