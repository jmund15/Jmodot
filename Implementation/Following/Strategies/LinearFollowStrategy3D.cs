namespace Jmodot.Implementation.Following.Strategies;

using Godot;

/// <summary>
/// Constant-magnitude pull toward the target, with a dead-zone near the
/// target that suppresses impulse to avoid jitter. Speed of approach is
/// independent of distance outside the dead-zone.
///
/// <para>
/// Tuning shape (BaseKey parity): designers set a single acceleration and
/// a dead-zone radius. Distance outside the dead-zone does not alter the
/// force magnitude — only the direction. Use <see cref="ProximityGravityFollowStrategy3D"/>
/// when closer = stronger is desired instead.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class LinearFollowStrategy3D : FollowStrategy3D
{
    /// <summary>
    /// Acceleration magnitude applied each frame toward the target (units/sec²).
    /// Integrated by delta, so the per-frame impulse is <c>Acceleration * delta</c>.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,200.0,0.1,suffix:u/s²")]
    public float Acceleration { get; set; } = 12f;

    /// <summary>
    /// If the follower is within this radius of the target, no pull impulse
    /// is applied (suppresses jitter as the follower settles near the target).
    /// </summary>
    [Export(PropertyHint.Range, "0.0,5.0,0.01,suffix:m")]
    public float DeadZoneRadius { get; set; } = 0.1f;

    public override Vector3 ComputeImpulse(
        ref FollowState3D state,
        Vector3 followerPosition,
        Vector3 targetPosition,
        Vector3 followerVelocity,
        float delta)
    {
        var toTarget = targetPosition - followerPosition;
        var distance = toTarget.Length();

        state.ElapsedTime += delta;
        state.LastDistance = distance;

        if (distance <= DeadZoneRadius || distance <= 0f)
        {
            return Vector3.Zero;
        }

        var direction = toTarget / distance;
        return direction * Acceleration * delta;
    }

    #region Test Helpers
#if TOOLS
    internal void SetAcceleration(float value) => Acceleration = value;
    internal void SetDeadZoneRadius(float value) => DeadZoneRadius = value;
#endif
    #endregion
}
