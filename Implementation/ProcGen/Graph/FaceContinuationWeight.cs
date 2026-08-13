namespace Jmodot.Implementation.ProcGen.Graph;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.ProcGen.Spatial;
using Jmodot.Implementation.ProcGen.Spatial;

/// <summary>
///     SOFT placement bias that favours ports whose outward span continues away from the layout
///     already committed around the anchor: <c>weight = 1 + StraightBonus</c> when the candidate
///     face opposes the anchor's entry face (the straight-through continuation), <c>1 + TurnBonus</c>
///     for a perpendicular (lateral) move, and <c>1</c> (neutral) when the candidate's outward cell
///     would overlap an already-placed footprint — the fold/collision the stage-2 embedder
///     (design-se §4) rejects. A geometrically bad port pick is otherwise only discovered when the
///     WHOLE floor fails to embed and re-rolls, so this keeps bad picks rare instead of absorbing a
///     full-floor re-roll. Stateless per the <see cref="SlotWeight" /> contract (no mutable state
///     beyond the <c>[Export]</c>s). Requires no metrics — the spine pass needs the bias before the
///     metrics snapshot exists.
///     <para>
///         Geometry is reconstructed in graph space: the generator commits every node through a
///         single parent edge (spine/route/branch, all inserted spine-first), so a BFS from the
///         Source over <see cref="PartialGraph" /> edges in insertion order yields each node's world
///         pose via <see cref="SpatialPoseMath.SolveAbutment"/> — the same arithmetic the embedder
///         uses — without inventing any API. Reconstruction is conservative (declared ports, no
///         embedder rebinding); a placement that survives it is geometrically simple, and one that
///         folds here is flagged neutral even though the embedder might rebind past it.
///     </para>
/// </summary>
[GlobalClass, Tool]
public sealed partial class FaceContinuationWeight : SlotWeight
{
    /// <summary>Extra weight for the straight-through continuation (candidate face opposes the anchor's entry face). 0 = neutral.</summary>
    [Export(PropertyHint.Range, "0,32")]
    public int StraightBonus { get; set; } = 8;

    /// <summary>Extra weight for a perpendicular (lateral) move relative to the entry face. 0 = neutral.</summary>
    [Export(PropertyHint.Range, "0,16")]
    public int TurnBonus { get; set; } = 3;

    /// <summary>Reads no metrics — reconstructs geometry from the graph itself, so it stays active in the pre-Sink spine pass.</summary>
    public override bool RequiresMetrics => false;

    internal override int Weight(in Placement p, PartialGraph g)
    {
        if (p.Slot is not PortSlot slot)
        {
            return 1; // not a port attachment — no geometry to score
        }

        if (slot.Port is not ISpatialPort candidatePort)
        {
            return 1; // non-spatial template (bare graph ports) — neutral, keeps the draw uniform
        }

        if (slot.Node.Template is not ISpatialNodeTemplate)
        {
            return 1;
        }

        if (g.Source == null)
        {
            return 1; // pre-spine — nothing committed to continue away from
        }

        if (!TryReconstruct(g, out Dictionary<StringName, CellPose> poses, out Dictionary<StringName, StringName> parentPorts))
        {
            return 1;
        }

        if (!poses.TryGetValue(slot.Node.Id, out CellPose anchorPose))
        {
            return 1;
        }

        // The Source has no predecessor — its exit sets the floor's initial direction, so keep it
        // un-biased (this is the seed-varying degree of freedom the port-selection draw relies on).
        if (!parentPorts.TryGetValue(slot.Node.Id, out StringName parentPortName))
        {
            return 1;
        }

        if (PortByName(slot.Node.Template, parentPortName) is not ISpatialPort anchorEntry)
        {
            return 1;
        }

        if (p.Template is not ISpatialNodeTemplate newTemplate)
        {
            return 1;
        }

        // The new node's entry port mirrors SelectOpenPort: first port whose type matches the anchor
        // port. Determines where the new footprint actually lands.
        IGraphPort? entry = FirstTypeCompatible(p.Template, slot.Port.Type);
        if (entry is not ISpatialPort entrySpatial)
        {
            return 1;
        }

        PortFace candidateWorldFace = SpatialPoseMath.RotateFace(candidatePort.Face, anchorPose.Yaw);
        YawQuadrant? newYaw = YawForWorldFace(entrySpatial.Face, candidateWorldFace);
        if (newYaw == null)
        {
            return 1; // candidate and template faces cannot abut
        }

        WorldPort candidateWorld = SpatialPoseMath.WorldPortOf(anchorPose, ((ISpatialNodeTemplate)slot.Node.Template).FootprintCells, candidatePort);
        Vector3I? origin = SpatialPoseMath.SolveAbutment(candidateWorld, entrySpatial, newTemplate.FootprintCells, newYaw.Value);
        if (origin == null)
        {
            return 1;
        }

        if (OverlapsAny(origin.Value, newYaw.Value, newTemplate, poses, g))
        {
            return 1; // the outward cell folds into the committed layout — embed would reject it
        }

        PortFace entryWorldFace = SpatialPoseMath.RotateFace(anchorEntry.Face, anchorPose.Yaw);
        if (candidateWorldFace == SpatialPoseMath.Opposite(entryWorldFace))
        {
            return Math.Max(1, 1 + this.StraightBonus);
        }

        if (candidateWorldFace == entryWorldFace)
        {
            return 1; // doubles straight back toward the predecessor — the tightest fold
        }

        return Math.Max(1, 1 + this.TurnBonus);
    }

    private static IGraphPort? PortByName(INodeTemplate template, StringName name)
    {
        foreach (IGraphPort port in template.Ports)
        {
            if (port.Name == name)
            {
                return port;
            }
        }

        return null;
    }

    private static IGraphPort? FirstTypeCompatible(INodeTemplate template, StringName requiredType)
    {
        foreach (IGraphPort port in template.Ports)
        {
            if (PortTypes.Matches(port.Type, requiredType))
            {
                return port;
            }
        }

        return null;
    }

    private static YawQuadrant? YawForWorldFace(PortFace portFace, PortFace anchorWorldFace)
    {
        PortFace target = SpatialPoseMath.Opposite(anchorWorldFace);
        for (int q = 0; q < 4; q++)
        {
            if (SpatialPoseMath.RotateFace(portFace, (YawQuadrant)q) == target)
            {
                return (YawQuadrant)q;
            }
        }

        return null;
    }

    private static bool OverlapsAny(
        Vector3I origin, YawQuadrant yaw, ISpatialNodeTemplate template,
        IReadOnlyDictionary<StringName, CellPose> poses, PartialGraph g)
    {
        Vector3I size = SpatialPoseMath.RotateSize(template.FootprintCells, yaw);
        foreach (GraphNode node in g.Nodes)
        {
            if (!poses.TryGetValue(node.Id, out CellPose other) || node.Template is not ISpatialNodeTemplate otherTemplate)
            {
                continue;
            }

            Vector3I otherSize = SpatialPoseMath.RotateSize(otherTemplate.FootprintCells, other.Yaw);
            if (BoxesIntersect(origin, size, other.Origin, otherSize))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BoxesIntersect(Vector3I aOrigin, Vector3I aSize, Vector3I bOrigin, Vector3I bSize)
        => aOrigin.X < bOrigin.X + bSize.X && bOrigin.X < aOrigin.X + aSize.X
        && aOrigin.Z < bOrigin.Z + bSize.Z && bOrigin.Z < aOrigin.Z + aSize.Z;

    private static bool TryReconstruct(
        PartialGraph g,
        out Dictionary<StringName, CellPose> poses,
        out Dictionary<StringName, StringName> parentPorts)
    {
        poses = new Dictionary<StringName, CellPose>();
        parentPorts = new Dictionary<StringName, StringName>();

        if (g.Source == null || g.Source.Template is not ISpatialNodeTemplate)
        {
            return false;
        }

        poses[g.Source.Id] = new CellPose(Vector3I.Zero, YawQuadrant.Yaw0);

        // Flood from the Source over edges in insertion order, placing each node via its FIRST
        // reaching edge (the generator commits spine edges before decorations, so this matches how
        // the node was actually entered). Deterministic; O(N·E) is fine for floor-scale graphs.
        bool progressed;
        do
        {
            progressed = false;
            foreach (GraphNode node in g.Nodes)
            {
                if (!poses.ContainsKey(node.Id))
                {
                    continue;
                }

                foreach (GraphEdge edge in g.Edges)
                {
                    IGraphNode? other = null;
                    bool otherIsTo = false;
                    if (edge.From.Id == node.Id)
                    {
                        other = edge.To;
                        otherIsTo = true;
                    }
                    else if (edge.To.Id == node.Id)
                    {
                        other = edge.From;
                        otherIsTo = false;
                    }

                    if (other == null || poses.ContainsKey(other.Id) || other.Template is not ISpatialNodeTemplate otherTemplate)
                    {
                        continue;
                    }

                    if (TryPlaceFromEdge(node, edge, other, otherIsTo, poses, out CellPose pose, out StringName otherPort))
                    {
                        poses[other.Id] = pose;
                        parentPorts[other.Id] = otherPort;
                        progressed = true;
                    }
                }
            }
        }
        while (progressed);

        return true;
    }

    private static bool TryPlaceFromEdge(
        IGraphNode placed, GraphEdge edge, IGraphNode other, bool otherIsTo,
        IReadOnlyDictionary<StringName, CellPose> poses,
        out CellPose pose, out StringName otherPortName)
    {
        pose = default;
        otherPortName = default;

        if (placed.Template is not ISpatialNodeTemplate placedTemplate || other.Template is not ISpatialNodeTemplate otherTemplate)
        {
            return false;
        }

        IGraphPort? placedPortGraph = PortByName(placed.Template, otherIsTo ? edge.FromPort : edge.ToPort);
        IGraphPort? otherPortGraph = PortByName(other.Template, otherIsTo ? edge.ToPort : edge.FromPort);
        if (placedPortGraph is not ISpatialPort placedPort || otherPortGraph is not ISpatialPort otherPort)
        {
            return false;
        }

        CellPose placedPose = poses[placed.Id];
        WorldPort placedWorld = SpatialPoseMath.WorldPortOf(placedPose, placedTemplate.FootprintCells, placedPort);

        YawQuadrant? yaw = YawForWorldFace(otherPort.Face, placedWorld.Face);
        if (yaw == null)
        {
            return false;
        }

        Vector3I? origin = SpatialPoseMath.SolveAbutment(placedWorld, otherPort, otherTemplate.FootprintCells, yaw.Value);
        if (origin == null)
        {
            return false;
        }

        pose = new CellPose(origin.Value, yaw.Value);
        otherPortName = otherPortGraph.Name;
        return true;
    }
}
