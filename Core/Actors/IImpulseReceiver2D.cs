namespace Jmodot.Core.Actors;

/// <summary>
/// The write-only impulse channel of a 2D movement pipeline. Consumers that may only nudge an
/// agent — movement quirks above all — are typed against this rather than
/// <see cref="IMovementProcessor2D" />, so the contract "writes only impulses" is enforced by the
/// type system instead of by convention: there is no strategy override or tick to reach for.
/// Dimension-parallel sibling: <see cref="IImpulseReceiver3D" />.
/// </summary>
public interface IImpulseReceiver2D
{
    /// <param name="impulse">Velocity-delta, not an impulse in N·s.</param>
    /// <param name="mode">
    /// Whether this composes with the frame's other velocity or replaces it. See
    /// <see cref="ImpulseMode" /> — the Replace case cannot be arranged from the call site.
    /// </param>
    void ApplyImpulse(Vector2 impulse, ImpulseMode mode = ImpulseMode.Add);
}
