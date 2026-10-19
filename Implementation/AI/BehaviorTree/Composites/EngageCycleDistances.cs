namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using Core.AI;
using Goals = Jmodot.Implementation.AI.Navigation.Goals;

/// <summary>
/// The pure number comparisons an engage cycle makes across its slots. Each returns a warning
/// naming both slots, or null when the pair is consistent or one side is missing. Only two member
/// bands are comparable; a set band describes a different question and is skipped.
/// </summary>
public static class EngageCycleDistances
{
    /// <summary>Slack on the position-versus-engage gap, so float sums of authored metres compare as authored.</summary>
    public const float PositionTolerance = 0.01f;

    /// <summary>
    /// Warns when the engage band's far edge clears the position ring's far edge by less than
    /// <paramref name="clearance"/>: a creature handed onto its ring at speed coasts past the gate
    /// and leaves the cycle. A clearance that is negative or not finite warns on its own.
    /// </summary>
    public static string? PositionVsEngage(Goals.GoalDistanceBand? position, RangeBandResource? engage, float clearance)
    {
        if (!float.IsFinite(clearance) || clearance < 0f)
        {
            return $"Gate Clearance ({clearance}) must be zero or a positive, finite number of metres.";
        }
        if (!IsMemberBand(position) || engage == null) { return null; }
        var ring = position!.Value;
        if (!float.IsFinite(ring.Max) || !float.IsFinite(engage.MaxRange)) { return null; }
        float gap = engage.MaxRange - ring.Max;
        if (gap > PositionTolerance && gap >= clearance - PositionTolerance) { return null; }

        return $"Position ring reaches {ring.Max:0.##} m, {engage.MaxRange - ring.Max:0.##} m inside the engage " +
               $"band's {engage.MaxRange:0.##} m edge, but Gate Clearance asks for {clearance:0.##} m. A creature " +
               "handed onto the ring at speed coasts past the gate and leaves the cycle; raise the engage band, " +
               "lower the Position ring, or set Gate Clearance from the creature's speed.";
    }

    /// <summary>Warns when the commit band is not contained in the attack band.</summary>
    public static string? CommitVsAttack(Goals.GoalDistanceBand? commit, RangeBandResource? attack)
    {
        if (!IsMemberBand(commit) || attack == null) { return null; }
        var band = commit!.Value;
        if (!float.IsFinite(band.Min) || !float.IsFinite(band.Max)) { return null; }
        if (band.Min >= attack.MinRange && band.Max <= attack.MaxRange) { return null; }

        return $"Commit band [{band.Min:0.##}, {band.Max:0.##}] m is not inside the attack band " +
               $"[{attack.MinRange:0.##}, {attack.MaxRange:0.##}] m. Commit would arrive outside the " +
               "range the attack can fire from.";
    }

    /// <summary>
    /// Warns when the back-out threshold (the recover band's near edge, not infinity) falls outside
    /// the position ring.
    /// </summary>
    public static string? BackOutVsRing(Goals.GoalDistanceBand? backOut, Goals.GoalDistanceBand? ring)
    {
        if (!IsMemberBand(backOut) || !IsMemberBand(ring)) { return null; }
        var threshold = backOut!.Value.Min;
        var position = ring!.Value;
        if (!float.IsFinite(threshold) || !float.IsFinite(position.Min) || !float.IsFinite(position.Max))
        {
            return null;
        }
        if (threshold >= position.Min && threshold <= position.Max) { return null; }

        return $"Recover backs out to {threshold:0.##} m, which is outside the Position ring " +
               $"[{position.Min:0.##}, {position.Max:0.##}] m. The next cycle would reposition from " +
               "outside its own ring.";
    }

    private static bool IsMemberBand(Goals.GoalDistanceBand? band)
        => band is { } value && value.Geometry
            == Jmodot.Implementation.AI.Navigation.Targets.TargetSampleGeometry.Member;
}
