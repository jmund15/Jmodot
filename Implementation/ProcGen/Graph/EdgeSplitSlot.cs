namespace Jmodot.Implementation.ProcGen.Graph;

using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     A <see cref="CandidateSlot" /> for an existing edge the generator may split by inserting a node
///     mid-edge. Anchored to the edge's <c>From</c>. Id is
///     <c>split{Sep}{From.Id}{Sep}{FromPort}{Sep}{To.Id}{Sep}{ToPort}</c> — direction-sensitive, matching
///     the direction-sensitive GraphSignature oracle.
/// </summary>
public sealed class EdgeSplitSlot : CandidateSlot
{
    public EdgeSplitSlot(GraphEdge edge)
        : base(new StringName($"split{Sep}{edge.From.Id}{Sep}{edge.FromPort}{Sep}{edge.To.Id}{Sep}{edge.ToPort}"))
    {
        this.Edge = edge;
    }

    public GraphEdge Edge { get; }

    public override IGraphNode AnchorNode => this.Edge.From;
}
