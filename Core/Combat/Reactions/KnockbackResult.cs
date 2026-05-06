namespace Jmodot.Core.Combat.Reactions;

using Godot;

/// <summary>
/// Result type produced by KnockbackEffect. Implements <see cref="IForceCarrier"/> so that
/// force-receiving subscribers (KnockbackComponent3D, etc.) treat it identically to any
/// other kinetic-energy-bearing CombatResult — no per-subtype dispatch needed.
/// </summary>
public record KnockbackResult : CombatResult, IForceCarrier
{
    public Vector3 Direction { get; init; }
    public float Force { get; init; }

    /// <summary>
    /// Output bit set post-hoc by the receiver after the launch-threshold check
    /// (e.g., KnockbackComponent3D promotes to true when impulse exceeds LaunchThreshold).
    /// Producers initialize this false; consumers mutate via <c>result with { TriggeredLaunch = true }</c>.
    /// </summary>
    public bool TriggeredLaunch { get; init; }
}
