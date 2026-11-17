namespace Jmodot.Implementation.ProcGen.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     Progressive embedding session (Design A): wraps a FROZEN backbone embed state and lets the
///     generator validate decorations against real grid geometry before committing them. Created from a
///     backbone graph (its spine is embedded + frozen on construction); each accepted loop/branch is
///     trial-embedded onto a clone and adopted only on success, so the session always holds a fully
///     embeddable layout. <see cref="BuildResult" /> emits the final layout reusing every frozen pose.
///     Stateful (unlike the stateless <see cref="GridFloorEmbedder" />) — one session per floor.
/// </summary>
internal sealed class LayoutSession : ILayoutAdvisor
{
    private readonly GridFloorEmbedder _embedder;
    private readonly GeometryEnvelope _envelope;
    private readonly EmbedderSettings _settings;
    private GridFloorEmbedder.SearchState _state;
    private IFloorGraph _committed;

    internal LayoutSession(
        GridFloorEmbedder embedder,
        IFloorGraph backbone,
        GeometryEnvelope envelope,
        EmbedderSettings settings,
        ConnectorRules rules)
    {
        ArgumentNullException.ThrowIfNull(embedder);
        ArgumentNullException.ThrowIfNull(backbone);
        this._embedder = embedder;
        this._envelope = envelope;
        this._settings = settings;
        this._state = embedder.BuildState(backbone, envelope, settings, rules);
        this._committed = backbone;
    }

    /// <summary>
    ///     The embed failure of the backbone this session was opened on, or null when every backbone room placed.
    ///     A failed session holds only part of its frozen frame: every <see cref="GridFloorEmbedder.Extend" /> on it
    ///     returns this failure, so <see cref="TryCommitSubgraph" /> refuses every decoration and
    ///     <see cref="BuildResult" /> returns it, and the pipeline re-rolls the attempt.
    /// </summary>
    public FloorEmbedResult? BackboneFailure => this._state.BackboneFailure;

    /// <summary>
    ///     Shortest corridor length between two PLACED rooms' ports, or null if either room is not yet committed
    ///     or no corridor joins the two doors. The generator orders anchor pairs by this so a route is tried first
    ///     between doors that face each other.
    /// </summary>
    public int? BridgeEstimate(StringName a, StringName aPort, StringName b, StringName bPort)
    {
        WorldSpan? from = this.PlacedPortWorld(a, aPort);
        WorldSpan? to = this.PlacedPortWorld(b, bPort);
        if (!from.HasValue || !to.HasValue)
        {
            return null;
        }

        (WorldPort AWindow, WorldPort BWindow)? windows = SpatialPoseMath.ConnectorWindows(from.Value, to.Value);
        return windows.HasValue
            ? ConnectorSolver.EstimateBridgeCells(windows.Value.AWindow, windows.Value.BWindow, this._state.Rules.Policy)
            : null;
    }

    /// <summary>The openable run of a placed node port in world cells, or null when the node is not placed or has no such port.</summary>
    internal WorldSpan? PlacedPortWorld(StringName nodeId, StringName portName)
    {
        if (!this._state.Poses.TryGetValue(nodeId, out CellPose pose))
        {
            return null;
        }

        IGraphNode? node = this._committed.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node?.Template is not ISpatialNodeTemplate spatial)
        {
            return null;
        }

        ISpatialPort? port = node.Template.Ports.OfType<ISpatialPort>().FirstOrDefault(p => p.NameOf() == portName);
        return port == null ? null : SpatialPoseMath.SpanOf(pose, spatial.FootprintCells, port);
    }

    /// <summary>
    ///     Committed nodes that still expose at least one UNBOUND template port — candidate attach points
    ///     for a branch or a loop anchor. A coarse pre-filter (it does not check whether the port opens
    ///     into free space); <see cref="TryCommitSubgraph" /> is the authoritative feasibility gate.
    /// </summary>
    public IReadOnlyList<StringName> NodesWithFreeSpur()
    {
        var result = new List<StringName>();
        foreach (IGraphNode node in this._committed.Nodes)
        {
            if (!this._state.Poses.ContainsKey(node.Id))
            {
                continue;
            }

            if (node.Template.Ports.Any(p => !this._state.BoundPorts.Contains((node.Id, p.Name))))
            {
                result.Add(node.Id);
            }
        }

        return result;
    }

    /// <summary>
    ///     Authoritatively validates the decoration nodes in <paramref name="graphSoFar" /> (those not yet
    ///     placed) by trial-embedding them onto a CLONE of the committed state, searching on
    ///     <see cref="EmbedderSettings.TrialRepairBudget" /> rather than the floor's budget. On success the clone is
    ///     adopted (the decoration's poses join the frozen set, so later placements pack around it) and
    ///     <paramref name="graphSoFar" /> becomes the committed graph; on failure nothing changes and the
    ///     caller drops/retries the decoration. <paramref name="graphSoFar" /> must share node/edge
    ///     instances with the backbone (see <see cref="GridFloorEmbedder.Extend" />).
    /// </summary>
    public bool TryCommitSubgraph(IFloorGraph graphSoFar)
    {
        ArgumentNullException.ThrowIfNull(graphSoFar);
        GridFloorEmbedder.SearchState trial = this._state.Clone();
        FloorEmbedResult result = this._embedder.Extend(trial, graphSoFar, this._envelope, this._settings, this._settings.TrialRepairBudget);
        if (!result.Succeeded)
        {
            return false;
        }

        this._state = trial;
        this._committed = graphSoFar;
        return true;
    }

    /// <summary>
    ///     Emits the final layout from the accumulated frozen poses. <paramref name="fullGraph" /> is the
    ///     generator's complete topology; any node not yet committed is placed now (normally none — every
    ///     decoration was already trial-committed), then the whole layout is returned.
    /// </summary>
    public FloorEmbedResult BuildResult(IFloorGraph fullGraph)
        => this._embedder.Extend(this._state, fullGraph, this._envelope, this._settings);

    #region Test Helpers
#if TOOLS
    /// <summary>
    ///     The committed layout at one moment: poses and bound ports copied, the occupancy index and committed graph shared. A committed
    ///     trial replaces the session's state rather than mutating it, so a taken snapshot keeps its meaning.
    /// </summary>
    internal readonly record struct LayoutSnapshot(
        IReadOnlyDictionary<StringName, CellPose> Poses,
        IReadOnlySet<(StringName NodeId, StringName PortName)> BoundPorts,
        OccupancyIndex Occupancy,
        IFloorGraph Graph);

    internal LayoutSnapshot Snapshot => new(
        new Dictionary<StringName, CellPose>(this._state.Poses),
        new HashSet<(StringName NodeId, StringName PortName)>(this._state.BoundPorts),
        this._state.Occupancy,
        this._committed);
#endif
    #endregion
}
