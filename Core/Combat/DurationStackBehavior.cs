namespace Jmodot.Core.Combat;

/// <summary>
/// Defines how duration is handled when stacking effects with the same tag.
/// </summary>
public enum DurationStackBehavior
{
    /// <summary>Each instance has independent duration timers.</summary>
    Independent = 0,

    /// <summary>Add incoming duration to existing (up to MaxTotalDuration).</summary>
    Extend = 1,

    /// <summary>Reset duration to incoming value (no OnEnd/OnStart re-trigger).</summary>
    Refresh = 2,

    /// <summary>Keep whichever duration is longer.</summary>
    Max = 3,

    /// <summary>Reject if same tag already active.</summary>
    Reject = 4
}
