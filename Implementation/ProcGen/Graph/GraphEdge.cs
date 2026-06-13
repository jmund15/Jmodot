namespace Jmodot.Implementation.ProcGen.Graph;

using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Immutable concrete <see cref="IGraphEdge" />. Plain CLR (no Godot base).
/// </summary>
public sealed class GraphEdge : IGraphEdge
{
    /// <param name="provenance">
    ///     Which generation pass laid this edge. The default (<c>EdgeProvenanceKind.Unset</c>) is
    ///     rejected at <see cref="FloorGraph" /> construction and <c>GraphSignature.Of</c> — always
    ///     stamp a concrete provenance before the edge reaches graph assembly.
    /// </param>
    public GraphEdge(
        IGraphNode from,
        StringName fromPort,
        IGraphNode to,
        StringName toPort,
        bool isGated,
        EdgeTraversal traversal,
        EdgeProvenance provenance = default)
    {
        this.From = from;
        this.FromPort = fromPort;
        this.To = to;
        this.ToPort = toPort;
        this.IsGated = isGated;
        this.Traversal = traversal;
        this.Provenance = provenance;
    }

    public IGraphNode From { get; }

    public StringName FromPort { get; }

    public IGraphNode To { get; }

    public StringName ToPort { get; }

    public bool IsGated { get; }

    public EdgeTraversal Traversal { get; }

    public EdgeProvenance Provenance { get; }
}
