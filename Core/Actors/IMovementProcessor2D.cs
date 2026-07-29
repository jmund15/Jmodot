namespace Jmodot.Core.Actors;

using Movement.Strategies;

public interface IMovementProcessor2D : IImpulseReceiver2D
{
    void ProcessMovement(IMovementStrategy2D strategy2D, Vector2 desiredDirection, float delta);
    void ProcessExternalForcesOnly(float delta);
    void ClearImpulses();

    /// <summary>
    /// Claim exclusive positional authority, suspending the processor's own movement pump.
    /// While suspended the owner is responsible for the body's position: the strategy, gravity,
    /// external forces, velocity offsets and Move() are all skipped by every pump entry point.
    /// Impulses are DISCARDED, not queued — the claim clears the pending-impulse accumulator and
    /// each suspended tick clears it again, so only an impulse applied after release ever lands.
    /// Returns false (and warns) when a different owner already holds the claim; re-claiming as
    /// the current owner is idempotent and succeeds.
    /// </summary>
    /// <param name="velocityPolicy">Whether the claim also zeroes the controller's velocity. Preserve by default —
    /// zeroing is opt-in, for a claimant whose release must not resume the body's pre-claim vector.</param>
    bool TryClaimSuspension(StringName owner, SuspensionVelocityPolicy velocityPolicy = SuspensionVelocityPolicy.Preserve);

    /// <summary>
    /// Release a suspension claim. No-op + Warning when the caller does not hold it.
    /// The claim is not reference-counted — one release by the owner clears it.
    /// </summary>
    void ReleaseSuspension(StringName owner);

    /// <summary>True while a suspension claim is held and every pump entry point is short-circuited.</summary>
    bool IsSuspended { get; }
}
