namespace Jmodot.Implementation.AI.BehaviorTree.Tasks.DitherPicks;

using System;
using Core.AI.BehaviorTree;

/// <summary>
/// Right-left-right-left: successive flips alternate between one member of the set's circular ring
/// and the member half a ring away from it, so the agent weaves across a fixed axis rather than
/// wandering. Ignores the stream entirely — the sequence is a function of the flip index alone.
/// </summary>
[GlobalClass, Tool]
public partial class AlternatingDitherPick : DitherPickStrategy
{
    /// <inheritdoc />
    /// <remarks>
    /// The ring is angle-sorted, so index 0 and index <c>count / 2</c> are the two ends of the widest
    /// available axis. On a two-member set the ring degrades to authored order and the pick alternates
    /// between the two entries; a single-member set has no axis to weave across and returns that member
    /// on every flip.
    /// </remarks>
    public override Vector3 Pick(in DitherPickContext ctx)
    {
        var ordered = ctx.Directions.OrderedDirections;
        if (ordered.Count == 0) { return Vector3.Zero; }

        int opposite = Math.Max(1, ordered.Count / 2);
        int index = (ctx.FlipIndex & 1) == 0 ? 0 : opposite % ordered.Count;
        return ordered[index];
    }
}
