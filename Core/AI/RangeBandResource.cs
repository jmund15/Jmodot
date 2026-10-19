namespace Jmodot.Core.AI;

using Godot;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
/// A reusable [min, max] distance band used by AI components that gate or bias behavior by the
/// agent-to-target horizontal distance. Replaces the earlier practice of mirroring a
/// <c>MinRange</c> / <c>MaxRange</c> float pair across multiple components (a range gate that
/// requires the band plus the matching band exports on an approach action) — a footgun that left
/// the gate and the action's bands out of sync.
///
/// <para><b>Two-band authoring:</b> a single enemy scene typically authors <i>two</i> distinct
/// <see cref="RangeBandResource"/> instances: one for "inside range" (a required gate's check) and
/// one for the standoff distance an approach action treats as optional. They may be identical or
/// deliberately asymmetric; the type simply ensures each value is declared once and both consumers
/// share the same concrete band reference.</para>
///
/// <para><b>Validation:</b> <see cref="IsValid"/> rejects null, inverted order
/// (<c>MinRange &gt; MaxRange</c>), equal bounds (zero-width), and negative <c>MinRange</c>. A
/// consumer for which the band is required throws at initialization through
/// <see cref="RequireUsable"/>; one for which it is optional guards with <see cref="IsValid"/> and
/// falls back to its own default.</para>
/// </summary>
[GlobalClass, Tool]
public partial class RangeBandResource : Resource
{
    /// <summary>Inclusive lower bound of the band, in meters.</summary>
    [Export(PropertyHint.Range, "0.0,100.0,0.5,suffix:m")]
    public float MinRange { get; private set; } = 6f;

    /// <summary>Inclusive upper bound of the band, in meters.</summary>
    [Export(PropertyHint.Range, "0.0,100.0,0.5,suffix:m")]
    public float MaxRange { get; private set; } = 12f;

    /// <summary>
    /// Returns true when the band is null-safe AND its bounds are internally
    /// consistent (non-negative, <c>MinRange &lt; MaxRange</c>). Use as a
    /// precondition before consuming <see cref="MinRange"/> / <see cref="MaxRange"/>
    /// so misconfigured resources fail closed rather than producing ambiguous
    /// distance checks.
    /// </summary>
    public static bool IsValid(RangeBandResource? band)
    {
        if (band == null) { return false; }
        if (band.MinRange < 0f) { return false; }
        return band.MinRange < band.MaxRange;
    }

    /// <summary>
    /// Fails loud when a band a consumer requires is unusable (see <see cref="IsValid"/>), naming the slot
    /// that holds it. The caller guarantees <paramref name="band"/> is assigned: a required export validates
    /// first, and an optional slot is checked for null before it calls this.
    /// </summary>
    /// <exception cref="ResourceConfigurationException"><paramref name="band"/> is inverted, zero-width or starts below zero.</exception>
    public static void RequireUsable(RangeBandResource band, string slot)
    {
        if (UnusableWarning(band, slot) is { } warning) { throw new ResourceConfigurationException(warning, band); }
    }

    /// <summary>
    /// The one wording of an unusable band (see <see cref="IsValid"/>) under <paramref name="slot"/>, or null
    /// when the band is usable. The caller handles an unassigned slot first, since an empty slot means
    /// different things to different hosts.
    /// </summary>
    public static string? UnusableWarning(RangeBandResource band, string slot)
        => IsValid(band)
            ? null
            : $"{slot} is unusable: MinRange ({band.MinRange}) must be non-negative and strictly "
              + $"less than MaxRange ({band.MaxRange}).";

    /// <summary>
    /// The arrive test of an action that succeeds on entering a band: whether <paramref name="distance"/> lies inside
    /// <paramref name="band"/>. An unassigned band is never arrived at.
    /// </summary>
    public static bool HasArrived(float distance, RangeBandResource? band) => band != null && band.Contains(distance);

    /// <summary>Returns whether a distance lies inside this inclusive band.</summary>
    public bool Contains(float distance)
    {
        return Contains(distance, MinRange, MaxRange);
    }

    /// <summary>Returns whether a distance lies inside an inclusive range.</summary>
    public static bool Contains(float distance, float minRange, float maxRange)
    {
        if (!float.IsFinite(distance) || !float.IsFinite(minRange) || !float.IsFinite(maxRange)) { return false; }
        if (minRange > maxRange) { return false; }
        return distance >= minRange && distance <= maxRange;
    }

    #region Test Helpers
#if TOOLS
    internal void SetRangesForTest(float min, float max)
    {
        MinRange = min;
        MaxRange = max;
    }
#endif
    #endregion
}
