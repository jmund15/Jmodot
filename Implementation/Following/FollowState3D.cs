namespace Jmodot.Implementation.Following;

/// <summary>
/// Per-follower mutable runtime state for a <see cref="FollowStrategy3D"/>.
/// Variant A from the Factory→Runner pattern — the strategy is stateless
/// (shared across consumers via a single <c>.tres</c>), mutable per-follow
/// state lives in this struct owned by the consumer. Strategies mutate via
/// <c>ref</c> parameter.
///
/// <para>
/// Fields here are generic by design — any FollowStrategy3D or FollowModifier3D
/// subclass can read/write them. Strategy-specific state should only be added
/// here if cross-strategy reuse is plausible; otherwise, consumers should
/// layer their own state alongside.
/// </para>
/// </summary>
public struct FollowState3D
{
    /// <summary>
    /// Seconds elapsed since this follow engagement began. Strategies may use
    /// it for acceleration ramps or fade-in; modifiers may use it as a time
    /// basis when they lack their own phase accumulator.
    /// </summary>
    public float ElapsedTime;

    /// <summary>
    /// Distance from follower to target recorded during the last
    /// <see cref="FollowStrategy3D.ComputeImpulse"/> call. Strategies may use
    /// it for damping ramps that smooth speed as the follower approaches.
    /// </summary>
    public float LastDistance;

    /// <summary>
    /// Phase accumulator (radians) for periodic modifiers. Bounce, zigzag, and
    /// similar oscillators advance this each call so their motion remains
    /// continuous across frames without each modifier owning its own clock.
    /// </summary>
    public float Phase;
}
