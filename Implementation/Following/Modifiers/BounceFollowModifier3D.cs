namespace Jmodot.Implementation.Following.Modifiers;

using Godot;

/// <summary>
/// Adds a vertical sinusoidal oscillation to the impulse whose frequency
/// and amplitude scale with the follower's horizontal speed. Ports the feel
/// of the BaseKey reference behavior (faster horizontal motion ⇒ shorter,
/// higher-amplitude bounces) to the impulse-additive pipeline.
///
/// <para>
/// <b>Math:</b> phase advances at <c>BaseFrequency * speedRatio</c> Hz;
/// vertical velocity delta this frame is
/// <c>sin(phase) * BaseAmplitude * speedRatio * delta</c>. Integrated by the
/// consumer, this produces a wave-shaped vertical velocity, which in turn
/// produces a cosine-shaped position offset — exactly the hop feel we want.
/// </para>
///
/// <para>
/// Because the impulse channel is added to the follower's velocity each
/// frame, the oscillation is "real" — collisions, contact-radius pickups,
/// and any other velocity-aware systems observe the bounce. This is the
/// juice-aware-pickup behavior the design called for.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class BounceFollowModifier3D : FollowModifier3D
{
    /// <summary>
    /// Peak vertical acceleration (units/sec²) at <see cref="ReferenceSpeed"/>.
    /// Scales linearly with the follower's horizontal speed relative to
    /// <see cref="ReferenceSpeed"/>, clamped by <see cref="MinSpeedFactor"/>
    /// so the bounce never fully vanishes while the modifier is active.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,200.0,0.1,suffix:u/s²")]
    public float BaseAmplitude { get; set; } = 6f;

    /// <summary>
    /// Oscillation frequency (Hz) at <see cref="ReferenceSpeed"/>. Scales
    /// with the follower's horizontal speed.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,20.0,0.1,suffix:Hz")]
    public float BaseFrequency { get; set; } = 3f;

    /// <summary>
    /// The horizontal speed (units/sec) at which <see cref="BaseAmplitude"/>
    /// and <see cref="BaseFrequency"/> apply verbatim. Faster = brighter bounce;
    /// slower = gentler.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,20.0,0.1,suffix:u/s")]
    public float ReferenceSpeed { get; set; } = 2f;

    /// <summary>
    /// Lower bound on the speed-ratio multiplier so the bounce remains
    /// visible even when the follower is nearly stationary (e.g., starting
    /// a pull from rest). <c>0.1</c> keeps at least 10% of the reference
    /// amplitude/frequency.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float MinSpeedFactor { get; set; } = 0.1f;

    public override Vector3 ModifyImpulse(
        Vector3 baseImpulse,
        ref FollowState3D state,
        Vector3 followerVelocity,
        float delta)
    {
        var horizontalSpeed = new Vector2(followerVelocity.X, followerVelocity.Z).Length();
        var speedRatio = Mathf.Max(horizontalSpeed / Mathf.Max(ReferenceSpeed, 0.0001f), MinSpeedFactor);

        var frequency = BaseFrequency * speedRatio;
        var amplitude = BaseAmplitude * speedRatio;

        state.Phase += frequency * delta * Mathf.Tau;
        // Wrap the phase to stay in a bounded range — prevents float precision
        // drift if a single follower is tracked for many minutes.
        if (state.Phase > Mathf.Tau * 1000f)
        {
            state.Phase -= Mathf.Tau * 1000f;
        }

        var verticalDelta = Mathf.Sin(state.Phase) * amplitude * delta;
        return baseImpulse + new Vector3(0f, verticalDelta, 0f);
    }

    #region Test Helpers
#if TOOLS
    internal void SetBaseAmplitude(float value) => BaseAmplitude = value;
    internal void SetBaseFrequency(float value) => BaseFrequency = value;
    internal void SetReferenceSpeed(float value) => ReferenceSpeed = value;
    internal void SetMinSpeedFactor(float value) => MinSpeedFactor = value;
#endif
    #endregion
}
