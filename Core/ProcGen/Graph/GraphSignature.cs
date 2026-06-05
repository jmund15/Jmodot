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
///     exactly as stored — <c>(From.Id, FromPort, To.Id, ToPort, IsGated, Traversal)</c> — so a
///     reversed or re-gated edge is detected rather than masked. Ordering is removed by ordinal-sorting
///     the node-id strings and the edge-tuple strings independently; nothing is ever ordered by
///     reference identity (which would defeat cross-instance determinism).
///     </para>
/// </summary>
public static class GraphSignature
{
    // ASCII control chars chosen as delimiters because they never appear in authored node ids,
    // template ids, or port names — so a tuple boundary can never be forged by id content.
    private const char FieldSep = '\u001F';   // unit separator — between tuple members
    private const char RecordSep = '\u001E';  // record separator — between records within a section
    private const char SectionSep = '\u001D'; // group separator — between the node and edge sections

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
            .Select(EdgeTuple)
            .OrderBy(s => s, StringComparer.Ordinal);

        var builder = new StringBuilder();
        builder.Append(string.Join(RecordSep, nodeIds));
        builder.Append(SectionSep);
        builder.Append(string.Join(RecordSep, edgeTuples));
        return builder.ToString();
    }

    private static string EdgeTuple(IGraphEdge e)
        => string.Join(
            FieldSep,
            e.From.Id.ToString(),
            e.FromPort.ToString(),
            e.To.Id.ToString(),
            e.ToPort.ToString(),
            e.IsGated.ToString(),
            e.Traversal.ToString());
}
