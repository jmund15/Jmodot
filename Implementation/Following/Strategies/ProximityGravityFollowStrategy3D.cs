namespace Jmodot.Implementation.Following.Strategies;

using Godot;

/// <summary>
/// Distance-scaled pull — the closer the follower is to the target, the
/// stronger the pull. Models a "gravity well" feel: weak tug when barely in
/// range, snappy close-range convergence.
///
/// <para>
/// Magnitude is linearly interpolated from <see cref="MinAcceleration"/> at
/// <see cref="MaxRange"/> up to <see cref="MaxAcceleration"/> at
/// <see cref="DeadZoneRadius"/>. Beyond <see cref="MaxRange"/> the interpolator
/// clamps at <see cref="MinAcceleration"/> — you get the weak pull regardless
/// of how far out the follower is. Inside the dead-zone, no impulse is applied.
/// </para>
///
/// <para>
/// Use this instead of <see cref="LinearFollowStrategy3D"/> when the user
/// should feel attraction grow as they approach an item — the classic
/// magnet-pickup behavior.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class ProximityGravityFollowStrategy3D : FollowStrategy3D
{
    /// <summary>Acceleration at the far edge of the pull range (weakest pull).</summary>
    [Export(PropertyHint.Range, "0.0,200.0,0.1,suffix:u/s²")]
    public float MinAcceleration { get; set; } = 4f;

    /// <summary>Acceleration at (and inside) <see cref="DeadZoneRadius"/> (strongest pull).</summary>
    [Export(PropertyHint.Range, "0.0,200.0,0.1,suffix:u/s²")]
    public float MaxAcceleration { get; set; } = 20f;

    /// <summary>
    /// Distance at which the pull strength reaches <see cref="MinAcceleration"/>.
    /// Typically matches or is slightly less than the consumer's seek-radius
    /// so the pull is at-or-above MinAcceleration throughout the seeking zone.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,50.0,0.05,suffix:m")]
    public float MaxRange { get; set; } = 3f;

    /// <summary>
    /// Suppression radius near the target to prevent jitter. No impulse
    /// inside this radius.
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

        var range = Mathf.Max(MaxRange - DeadZoneRadius, 0.0001f);
        var t = Mathf.Clamp((distance - DeadZoneRadius) / range, 0f, 1f);
        var accel = Mathf.Lerp(MaxAcceleration, MinAcceleration, t);

        var direction = toTarget / distance;
        return direction * accel * delta;
    }

    #region Test Helpers
#if TOOLS
    internal void SetMinAcceleration(float value) => MinAcceleration = value;
    internal void SetMaxAcceleration(float value) => MaxAcceleration = value;
    internal void SetMaxRange(float value) => MaxRange = value;
    internal void SetDeadZoneRadius(float value) => DeadZoneRadius = value;
#endif
    #endregion
}
