namespace Jmodot.Implementation.ProcGen.Graph;

using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     HARD filter on whether the placed node's anchor sits on the critical path. A node is
///     on-path iff it is the graph Source, the Sink, or incident to a <c>CriticalEdge</c>
///     (membership is derived — <see cref="IGraphMetrics" /> exposes only the edge set).
///     <see cref="OnPath" /> selects the intent: <c>true</c> admits only on-path anchors,
///     <c>false</c> inverts to admit only off-path anchors. Engine-side, pure topology. Abstains
///     (admits) when metrics are unavailable in the pre-Sink spine pass. Comparison is by node
///     <c>Id</c> value, never reference (determinism).
/// </summary>
[GlobalClass, Tool]
public sealed partial class CritPathMembershipConstraint : SlotConstraint
{
    /// <summary>
    ///     When <c>true</c>, admit only candidates whose anchor is on the critical path; when
    ///     <c>false</c>, admit only candidates whose anchor is off it.
    /// </summary>
    [Export] public bool OnPath { get; set; }

    public override bool RequiresMetrics => true;

    internal override bool IsAdmissible(in Placement p, PartialGraph g)
    {
        if (!g.TryGetMetrics(out var metrics))
        {
            return true;
        }

        bool onPath = AnchorOnCritPath(p.Slot.AnchorNode, g, metrics);
        return this.OnPath ? onPath : !onPath;
    }

    private static bool AnchorOnCritPath(IGraphNode anchor, PartialGraph g, IGraphMetrics metrics)
    {
        StringName id = anchor.Id;

        if (g.Source != null && g.Source.Id == id)
        {
            return true;
        }

        if (g.Sink != null && g.Sink.Id == id)
        {
            return true;
        }

        foreach (var edge in metrics.CriticalEdges)
        {
            if (edge.From.Id == id || edge.To.Id == id)
            {
                return true;
            }
        }

        return false;
    }
}
