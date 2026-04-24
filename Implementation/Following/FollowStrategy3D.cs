namespace Jmodot.Implementation.Following;

using Godot;
using GCol = Godot.Collections;

/// <summary>
/// Abstract base Resource for "pull a follower toward a target" motion math
/// in 3D. Stateless by design — a single <c>.tres</c> can be shared across
/// any number of concurrent follows. Per-follower mutable state lives in
/// <see cref="FollowState3D"/> instances owned by the consumer.
///
/// <para>
/// <b>Portability contract:</b> this type and its subclasses are pure math.
/// They take <see cref="Vector3"/> positions and velocities in, return a
/// <see cref="Vector3"/> impulse out. They do NOT dispatch the impulse
/// themselves — the consumer decides where the resulting velocity delta
/// lands (a <c>MovementProcessor3D.ApplyImpulse</c>, a direct velocity
/// additive, an AI steering blend, etc.). Follow is a math library, not an
/// actor framework.
/// </para>
///
/// <para>
/// <b>Subclass rules:</b> concrete subclasses MUST be marked
/// <c>[GlobalClass, Tool]</c> — otherwise <c>.tres</c> files deserialize
/// as bare <see cref="Resource"/> and throw <see cref="System.InvalidCastException"/>
/// on type-checked access (see the Tool-attribute cascade rule).
/// </para>
///
/// <para>
/// <b>Composition via modifiers:</b> an optional <see cref="Modifiers"/>
/// array layers additional impulse contributions on top of the base
/// <see cref="ComputeImpulse"/> result. See <see cref="FollowModifier3D"/>.
/// Call <see cref="ComputeAndModifyImpulse"/> to run both in one step.
/// </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class FollowStrategy3D : Resource
{
    /// <summary>
    /// Optional list of juice/modulation layers applied in order after
    /// <see cref="ComputeImpulse"/>. Null or empty means no modulation.
    /// Null entries inside the array are skipped (matches the composite
    /// strategy precedent in the broader codebase).
    /// </summary>
    [Export] public GCol.Array<FollowModifier3D>? Modifiers { get; set; }

    /// <summary>
    /// Factory — produces a fresh per-follower runtime state. Called once
    /// when the consumer begins tracking a new follower. Default
    /// implementation returns a zero-initialized struct; override only if
    /// a non-zero seed is needed.
    /// </summary>
    public virtual FollowState3D CreateState() => default;

    /// <summary>
    /// Base motion contribution — the impulse this strategy wants to apply
    /// to the follower this frame, absent any modifiers.
    /// </summary>
    /// <param name="state">Mutable runtime state for this follower; the
    /// strategy may read and write fields (e.g., advance phase, cache distance).</param>
    /// <param name="followerPosition">World-space position of the thing being pulled.</param>
    /// <param name="targetPosition">World-space position of the pull target.</param>
    /// <param name="followerVelocity">Current velocity of the follower
    /// (strategies may use this to damp near the target or to
    /// cap acceleration).</param>
    /// <param name="delta">Physics-tick delta in seconds.</param>
    /// <returns>The velocity delta to apply this frame. Units: consumer-defined;
    /// typically treated as a one-shot velocity additive dispatched through an
    /// impulse pipeline.</returns>
    public abstract Vector3 ComputeImpulse(
        ref FollowState3D state,
        Vector3 followerPosition,
        Vector3 targetPosition,
        Vector3 followerVelocity,
        float delta);

    /// <summary>
    /// Convenience: computes the base impulse and then applies every non-null
    /// <see cref="Modifiers"/> entry in declared order. Consumers may call this
    /// instead of invoking <see cref="ComputeImpulse"/> + modifier iteration
    /// themselves — equivalent but keeps the modifier-array contract in one
    /// place for null-safety and ordering guarantees.
    /// </summary>
    public Vector3 ComputeAndModifyImpulse(
        ref FollowState3D state,
        Vector3 followerPosition,
        Vector3 targetPosition,
        Vector3 followerVelocity,
        float delta)
    {
        var impulse = ComputeImpulse(ref state, followerPosition, targetPosition, followerVelocity, delta);
        if (Modifiers == null)
        {
            return impulse;
        }
        foreach (var mod in Modifiers)
        {
            if (mod == null) { continue; }
            impulse = mod.ModifyImpulse(impulse, ref state, followerVelocity, delta);
        }
        return impulse;
    }
}
