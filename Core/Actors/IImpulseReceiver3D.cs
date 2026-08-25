namespace Jmodot.Core.Actors;

/// <summary>
/// The write-only impulse channel of a 3D movement pipeline. Consumers that may only nudge an
/// agent — movement quirks above all — are typed against this rather than
/// <see cref="IMovementProcessor3D" />, so the contract "writes only impulses" is enforced by the
/// type system instead of by convention: there is no strategy override or tick to reach for.
/// Dimension-parallel sibling: <see cref="IImpulseReceiver2D" />.
/// </summary>
public interface IImpulseReceiver3D
{
    /// <param name="impulse">Velocity-delta in m/s, not an N·s impulse.</param>
    /// <param name="mode">
    /// Whether this composes with the frame's other velocity or replaces it. See
    /// <see cref="ImpulseMode" /> — the Replace case cannot be arranged from the call site.
    /// </param>
    void ApplyImpulse(Vector3 impulse, ImpulseMode mode = ImpulseMode.Add);
}
