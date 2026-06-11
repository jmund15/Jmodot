namespace Jmodot.Core.ProcGen.Graph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
///     Topology-determinism oracle for an <see cref="IFloorGraph" />. <see cref="Of" /> emits a
///     canonical string that is a pure function of the graph's topology — identical for any two
///     graphs with the same nodes and edges regardless of construction order or CLR identity,
///     and different the moment any topology bit changes. Downstream determinism regression tests
///     pin a generated graph's signature against a golden value; a same-seed generator that drifts
///     produces a different signature.
///     <para>
///     Direction-sensitive by design (no bidirectional canonicalization): the edge tuple is emitted
///     exactly as stored — <c>(From.Id, FromPort, To.Id, ToPort, IsGated, Traversal, Provenance.Kind, Provenance.RouteOrdinal)</c> — so a
///     reversed or re-gated edge is detected rather than masked. Ordering is removed by ordinal-sorting
///     the node-id strings and the edge-tuple strings independently; nothing is ever ordered by
///     reference identity (which would defeat cross-instance determinism).
///     </para>
/// </summary>
public static class GraphSignature
{
    // ASCII control chars chosen as delimiters because they never appear in authored node ids,
    // template ids, or port names — so a tuple boundary can never be forged by id content.
    internal const char FieldSep = '\u001F';   // unit separator — between tuple members
    internal const char RecordSep = '\u001E';  // record separator — between records within a section
    internal const char SectionSep = '\u001D'; // group separator — between the node and edge sections

    /// <summary>
    ///     Produces the canonical topology signature of <paramref name="graph" />. Stable across
    ///     invocations, construction order, and distinct CLR instances of the same topology.
    /// </summary>
    public static string Of(IFloorGraph graph)
    {
        if (graph == null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        var nodeIds = graph.Nodes
            .Select(n => n.Id.ToString())
            .OrderBy(s => s, StringComparer.Ordinal);

        var edgeTuples = graph.Edges
            .Select(e => EdgeTupleCore(e, includePorts: true))
            .OrderBy(s => s, StringComparer.Ordinal);

        var builder = new StringBuilder();
        builder.Append(string.Join(RecordSep, nodeIds));
        builder.Append(SectionSep);
        builder.Append(string.Join(RecordSep, edgeTuples));
        return builder.ToString();
    }

    // The ONE canonical edge tuple every signature derives from: GraphSignature keeps the port
    // columns, TopologySignature projects them out. A future edge field added here joins every
    // signature at once and can never silently miss one of them.
    internal static string EdgeTupleCore(IGraphEdge e, bool includePorts)
    {
        if (e.Provenance.Kind == EdgeProvenanceKind.Unset)
        {
            throw new ArgumentException(
                $"Edge {e.From.Id}:{e.FromPort} -> {e.To.Id}:{e.ToPort} carries Unset provenance; a signature cannot be computed over an unstamped edge.");
        }

        var fields = new List<string>(8) { e.From.Id.ToString() };
        if (includePorts)
        {
            fields.Add(e.FromPort.ToString());
        }

        fields.Add(e.To.Id.ToString());
        if (includePorts)
        {
            fields.Add(e.ToPort.ToString());
        }

        fields.Add(e.IsGated.ToString());
        fields.Add(e.Traversal.ToString());
        fields.Add(e.Provenance.Kind.ToString());
        fields.Add(e.Provenance.RouteOrdinal.ToString());
        return string.Join(FieldSep, fields);
    }
}
