namespace Jmodot.Core.ProcGen.Graph;

using System;
using System.Linq;
using System.Text;

/// <summary>
///     Port-erased rebuild-equivalence oracle for an <see cref="IFloorGraph" />: sorted node ids
///     plus the edge multiset of <c>(From.Id, To.Id, IsGated, Traversal, Provenance)</c>, sharing
///     <see cref="GraphSignature" />'s canonical edge-tuple core with the port columns projected
///     out. Because the pipeline's published graph re-binds ports during embedding,
///     <c>TopologySignature.Of(preEmbed) == TopologySignature.Of(published)</c> is the only
///     invariant that catches edge loss or duplication across the rebuild — port-sensitive
///     <see cref="GraphSignature" /> legitimately differs there.
/// </summary>
public static class TopologySignature
{
    /// <summary>
    ///     Produces the canonical port-erased signature of <paramref name="graph" />. Stable across
    ///     invocations, construction order, distinct CLR instances, and port re-binding.
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
            .Select(e => GraphSignature.EdgeTupleCore(e, includePorts: false))
            .OrderBy(s => s, StringComparer.Ordinal);

        var builder = new StringBuilder();
        builder.Append(string.Join(GraphSignature.RecordSep, nodeIds));
        builder.Append(GraphSignature.SectionSep);
        builder.Append(string.Join(GraphSignature.RecordSep, edgeTuples));
        return builder.ToString();
    }
}
