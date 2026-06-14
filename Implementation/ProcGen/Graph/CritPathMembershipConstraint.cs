namespace Jmodot.Implementation.ProcGen.Graph;

using Jmodot.Core.ProcGen.Graph;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
///     HARD filter on whether the placed node's anchor sits on the critical path. A node is
///     on-path iff it is the graph Source, the Sink, or incident to a <c>CriticalEdge</c>
///     (membership is derived — <see cref="IGraphMetrics" /> exposes only the edge set).
///     <see cref="Side" /> selects the intent: <see cref="CritPathSide.OnCriticalPath" /> admits only
///     on-path anchors, <see cref="CritPathSide.OffCriticalPath" /> admits only off-path anchors, and
///     the invalid-at-zero <see cref="CritPathSide.Unset" /> is a misconfiguration that fails loud.
///     Engine-side, pure topology. Abstains (admits) when metrics are unavailable in the pre-Sink
///     spine pass. Comparison is by node <c>Id</c> value, never reference (determinism).
/// </summary>
[GlobalClass, Tool]
public sealed partial class CritPathMembershipConstraint : SlotConstraint
{
    /// <summary>
    ///     Which side of the critical path to admit. Defaults to <see cref="CritPathSide.Unset" />,
    ///     which is rejected at admissibility time — choose <see cref="CritPathSide.OnCriticalPath" />
    ///     or <see cref="CritPathSide.OffCriticalPath" /> explicitly (no silent default filter).
    /// </summary>
    [Export] public CritPathSide Side { get; set; }

    public override bool RequiresMetrics => true;

    internal override bool IsAdmissible(in Placement p, PartialGraph g)
    {
        if (this.Side == CritPathSide.Unset)
        {
            throw new ResourceConfigurationException(
                "CritPathMembershipConstraint.Side is Unset — choose OnCriticalPath or OffCriticalPath.", this);
        }

        if (!g.TryGetMetrics(out var metrics))
        {
            return true;
        }

        bool onPath = AnchorOnCritPath(p.Slot.AnchorNode, g, metrics);
        return this.Side == CritPathSide.OnCriticalPath ? onPath : !onPath;
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
