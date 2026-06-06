namespace Jmodot.Implementation.ProcGen.Graph;

using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.Shared;

/// <summary>
///     HARD filter admitting a candidate only when the placed node's depth — its anchor's
///     <c>DistanceFromSource</c> plus one hop (the new node attaches one hop downstream) — falls
///     within an inclusive <see cref="IntRange" />. Engine-side, pure topology (no project types).
///     Abstains (admits) when metrics are unavailable (pre-Sink spine pass) or the range is
///     unassigned; a <c>Min</c>/<c>Max</c> of <c>&lt;= 0</c> disables that bound, so a resave-to-0
///     degrades permissive rather than rejecting everything.
/// </summary>
[GlobalClass, Tool]
public sealed partial class DepthRangeConstraint : SlotConstraint
{
    /// <summary>
    ///     Inclusive depth window. Unassigned (null) disables the rule entirely; a bound of
    ///     <c>&lt;= 0</c> removes that side of the window (no floor / no ceiling).
    /// </summary>
    [Export] public IntRange? Range { get; set; }

    public override bool RequiresMetrics => true;

    internal override bool IsAdmissible(in Placement p, PartialGraph g)
    {
        if (this.Range == null)
        {
            return true;
        }

        if (!g.TryGetMetrics(out var metrics))
        {
            return true;
        }

        int depth = metrics.DistanceFromSource(p.Slot.AnchorNode) + 1;

        if (this.Range.Min > 0 && depth < this.Range.Min)
        {
            return false;
        }

        if (this.Range.Max > 0 && depth > this.Range.Max)
        {
            return false;
        }

        return true;
    }
}
