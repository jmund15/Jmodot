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

    /// <summary>
    /// Producer-set flag indicating the Direction's vertical component is INTENTIONAL
    /// and the receiver should NOT flatten it. Receivers like KnockbackComponent3D normally
    /// zero the Y component when their own <c>FlattenKnockback</c> is true (safety net for
    /// sloppy producers). When a producer explicitly biases the impulse upward — e.g.,
    /// KnockbackEffect with <c>UpwardAngleDegrees &gt; 0</c> for a rising rock pillar — it
    /// stamps this true so the receiver respects the source's authoritative direction.
    /// Defaults false to preserve existing flatten-on-receipt behavior for producers that
    /// don't opt in.
    /// </summary>
    public bool PreserveVertical { get; init; }
}
