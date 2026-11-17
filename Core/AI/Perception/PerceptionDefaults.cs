namespace Jmodot.Core.AI.Perception;

/// <summary>
/// Static seam for project-wide perception defaults. Consuming projects assign these once at startup (typically
/// from a registry autoload); Jmodot itself never assigns them, keeping the framework agnostic to any game's
/// physics-layer layout. Null means unwired: consumers fall back to their own authored values.
///
/// **Not thread-safe.** Wire once from the main thread at startup, before sensors run.
/// </summary>
public static class PerceptionDefaults
{
    /// <summary>
    /// The 3D physics layers that block a line-of-sight sensor's sightline when the sensor does not override its
    /// own mask. Line of sight exists only on the 3D area sensor today, so there is no 2D counterpart.
    /// </summary>
    public static uint? SightOcclusionMask3D { get; set; }

    /// <summary>Clears every wired default. Intended for test teardown; production code should not call this.</summary>
    internal static void Reset() => SightOcclusionMask3D = null;
}
