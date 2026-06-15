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

    internal LayoutSession(GridFloorEmbedder embedder, IFloorGraph backbone, GeometryEnvelope envelope, EmbedderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(embedder);
        ArgumentNullException.ThrowIfNull(backbone);
        this._embedder = embedder;
        this._envelope = envelope;
        this._settings = settings;
        this._state = embedder.BuildState(backbone, envelope, settings);
        this._committed = backbone;
    }

    /// <summary>
    ///     Manhattan grid distance (in cells, on the floor XZ plane) between two PLACED nodes' origins,
    ///     or null if either is not yet committed. The generator orders anchor-pair candidates by this so
    ///     a short route is offered exactly when the anchors are geometrically near — including the
    ///     spine-turns-back-on-itself case a graph-distance proxy cannot see.
    /// </summary>
    public int? GridStepDistance(StringName a, StringName b)
    {
        if (!this._state.Poses.TryGetValue(a, out CellPose poseA) || !this._state.Poses.TryGetValue(b, out CellPose poseB))
        {
            return null;
        }

        Vector3I delta = poseA.Origin - poseB.Origin;
        return Math.Abs(delta.X) + Math.Abs(delta.Z);
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
    ///     placed) by trial-embedding them onto a CLONE of the committed state. On success the clone is
    ///     adopted (the decoration's poses join the frozen set, so later placements pack around it) and
    ///     <paramref name="graphSoFar" /> becomes the committed graph; on failure nothing changes and the
    ///     caller drops/retries the decoration. <paramref name="graphSoFar" /> must share node/edge
    ///     instances with the backbone (see <see cref="GridFloorEmbedder.Extend" />).
    /// </summary>
    public bool TryCommitSubgraph(IFloorGraph graphSoFar)
    {
        ArgumentNullException.ThrowIfNull(graphSoFar);
        GridFloorEmbedder.SearchState trial = this._state.Clone();
        FloorEmbedResult result = this._embedder.Extend(trial, graphSoFar, this._envelope, this._settings);
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
}
