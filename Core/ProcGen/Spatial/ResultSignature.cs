namespace Jmodot.Core.ProcGen.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Full-result determinism oracle (design-se §5, recreated at P3b.5): composes
///     <see cref="GraphSignature" /> over the graph with an Id-sorted per-node
///     <c>(TemplateId, OriginCells, Yaw, SizeCells)</c> section and a canonically-sorted doorway
///     section — pose INCLUDES the yaw quadrant and doorways are first-class, the two axes the
///     retired pre-P3b version dropped. Sections share <see cref="GraphSignature" />'s delimiter
///     consts (the one canonical edge-tuple core); all ordering is ordinal, never emission or
///     dictionary order.
/// </summary>
public static class ResultSignature
{
    /// <summary>Signs a published pipeline result. The result must carry a graph (Succeeded).</summary>
    public static string Of(in FloorGenerationResult result)
    {
        if (result.Graph == null)
        {
            throw new ArgumentException("A failed result carries no graph; only successful results are signable.", nameof(result));
        }

        return Of(result.Graph, result.Layout, result.Doorways);
    }

    /// <summary>
    ///     Signs a (graph, layout, doorways) triple. Every graph node must have a layout entry —
    ///     the §5 identity invariant; a missing key fails loud rather than signing a partial result.
    /// </summary>
    public static string Of(
        IFloorGraph graph,
        IReadOnlyDictionary<StringName, CellPlacement> layout,
        IReadOnlyList<DoorwayPose> doorways)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(doorways);

        var nodeRecords = new List<string>(graph.Nodes.Count);
        foreach (IGraphNode node in graph.Nodes)
        {
            if (!layout.TryGetValue(node.Id, out CellPlacement placement))
            {
                throw new ArgumentException($"Layout carries no placement for node '{node.Id}'.", nameof(layout));
            }

            nodeRecords.Add(string.Join(
                GraphSignature.FieldSep,
                node.Id.ToString(),
                node.Template.TemplateId.ToString(),
                placement.OriginCells.X.ToString(),
                placement.OriginCells.Y.ToString(),
                placement.OriginCells.Z.ToString(),
                placement.Yaw.ToString(),
                placement.SizeCells.X.ToString(),
                placement.SizeCells.Y.ToString(),
                placement.SizeCells.Z.ToString()));
        }

        nodeRecords.Sort(StringComparer.Ordinal);

        var doorwayRecords = doorways
            .Select(d => string.Join(
                GraphSignature.FieldSep,
                d.FromNodeId.ToString(),
                d.ToNodeId.ToString(),
                d.FromPort.ToString(),
                d.ToPort.ToString(),
                d.SharedFaceOriginCells.X.ToString(),
                d.SharedFaceOriginCells.Y.ToString(),
                d.SharedFaceOriginCells.Z.ToString(),
                d.Face.ToString(),
                d.WidthCells.ToString()))
            .OrderBy(s => s, StringComparer.Ordinal);

        var builder = new StringBuilder();
        builder.Append(GraphSignature.Of(graph));
        builder.Append(GraphSignature.SectionSep);
        builder.Append(string.Join(GraphSignature.RecordSep, nodeRecords));
        builder.Append(GraphSignature.SectionSep);
        builder.Append(string.Join(GraphSignature.RecordSep, doorwayRecords));
        return builder.ToString();
    }
}
