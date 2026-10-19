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
    /// <summary>Slack on distance comparisons, so float sums of authored metres compare as authored.</summary>
    public const float PositionTolerance = 0.01f;

    /// <summary>
    /// Whether an engaged creature still holds its cycle: <paramref name="distance"/> lies inside
    /// <paramref name="engage"/> grown by <paramref name="clearance"/> on both edges. The band itself is the
    /// entry gate; the clearance is how far a creature already in the cycle may drift past it. An
    /// unusable clearance holds to the band itself, and an unmeasured distance never holds.
    /// </summary>
    public static bool HoldsEngagement(float distance, RangeBandResource engage, float clearance)
    {
        float grow = UsableClearance(clearance);
        return RangeBandResource.Contains(distance, engage.MinRange - grow, engage.MaxRange + grow);
    }

    /// <summary>
    /// Warns when a creature resting on the position ring's far edge, coasting its whole
    /// <paramref name="clearance"/>, would leave the hold <see cref="HoldsEngagement"/> grants. The hold grows
    /// the gate by the same clearance, so this asks that the ring sit inside the engage band. A clearance
    /// that is negative or not finite warns on its own.
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
        if (HoldsEngagement(ring.Max + clearance + PositionTolerance, engage, clearance)) { return null; }

        return $"Position ring reaches {ring.Max:0.##} m, at or past the engage band's {engage.MaxRange:0.##} m " +
               "edge. A creature resting on its ring would sit outside its own entry gate; lower the Position " +
               "ring or raise the engage band.";
    }

    /// <summary>
    /// Warns when the Position step carries a distance gate of its own that ends before the cycle's hold
    /// (the engage band grown by <paramref name="clearance"/>): that gate ejects the creature the hold
    /// keeps, so a creature coasting past the engage edge leaves the cycle anyway.
    /// </summary>
    public static string? PositionGateVsHold(RangeBandResource? positionGate, RangeBandResource? engage, float clearance)
    {
        if (positionGate == null || engage == null) { return null; }
        float hold = engage.MaxRange + UsableClearance(clearance);
        if (!float.IsFinite(positionGate.MaxRange) || positionGate.MaxRange >= hold - PositionTolerance) { return null; }

        return $"Position re-checks a distance band ending at {positionGate.MaxRange:0.##} m, inside the cycle's " +
               $"{hold:0.##} m hold (engage edge plus Gate Clearance). It drops a creature the cycle still holds; " +
               "remove it and let the cycle decide when the creature gives up.";
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

    private static float UsableClearance(float clearance) => float.IsFinite(clearance) && clearance > 0f ? clearance : 0f;

    private static bool IsMemberBand(Goals.GoalDistanceBand? band)
        => band is { } value && value.Geometry
            == Jmodot.Implementation.AI.Navigation.Targets.TargetSampleGeometry.Member;
}
