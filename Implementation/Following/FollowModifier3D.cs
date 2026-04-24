namespace Jmodot.Implementation.Following;

using Godot;

/// <summary>
/// Abstract base Resource for juice/modulation layers that transform the
/// impulse produced by a <see cref="FollowStrategy3D"/>. Bounce, zigzag,
/// accel-ramp, squash-stretch — anything that tweaks the base motion
/// without replacing it.
///
/// <para>
/// <b>Impulse-additive contract:</b> modifiers return a replacement impulse
/// (typically <c>baseImpulse + contribution</c>, but may also scale, rotate,
/// or clamp). The returned vector is then passed to the NEXT modifier in the
/// array (if any) as its <paramref name="baseImpulse"/>. Order matters.
/// </para>
///
/// <para>
/// <b>Stateless:</b> like <see cref="FollowStrategy3D"/>, modifiers are
/// stateless Resources. All mutable state lives in the shared
/// <see cref="FollowState3D"/> struct (see <see cref="FollowState3D.Phase"/>).
/// </para>
///
/// <para>
/// <b>Subclass rules:</b> concrete subclasses MUST be marked
/// <c>[GlobalClass, Tool]</c> — otherwise <c>.tres</c> files deserialize
/// as bare <see cref="Resource"/> and throw
/// <see cref="System.InvalidCastException"/> on type-checked access.
/// </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class FollowModifier3D : Resource
{
    /// <summary>
    /// Transform the impulse this frame.
    /// </summary>
    /// <param name="baseImpulse">The impulse produced by the strategy (or
    /// by previous modifiers in the chain).</param>
    /// <param name="state">Shared follow state; modifiers may advance
    /// <see cref="FollowState3D.Phase"/> or read <see cref="FollowState3D.ElapsedTime"/>.</param>
    /// <param name="followerVelocity">Current velocity of the follower;
    /// modifiers that scale by speed (e.g., bounce frequency-by-speed) read it here.</param>
    /// <param name="delta">Physics-tick delta in seconds.</param>
    /// <returns>The modified impulse.</returns>
    public abstract Vector3 ModifyImpulse(
        Vector3 baseImpulse,
        ref FollowState3D state,
        Vector3 followerVelocity,
        float delta);
}
