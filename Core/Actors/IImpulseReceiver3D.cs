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
    void ApplyImpulse(Vector3 impulse);
}
