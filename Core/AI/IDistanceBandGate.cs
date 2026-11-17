namespace Jmodot.Core.AI;

/// <summary>
/// A condition that gates on a distance band exposes that band, so a host can compare its authored
/// numbers against another slot's without knowing the condition's type or reverse-depending on it.
/// </summary>
public interface IDistanceBandGate
{
    /// <summary>The band this condition checks, or null when it has none.</summary>
    RangeBandResource? GateBand { get; }

    /// <summary>
    /// The distance this condition measures right now, or null when it has nothing to measure (no
    /// target). A host that holds a creature past the band reads this instead of <c>Check()</c>.
    /// </summary>
    float? MeasureDistance();
}
