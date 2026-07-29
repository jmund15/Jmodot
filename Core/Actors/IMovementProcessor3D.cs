namespace Jmodot.Core.Actors;

using Movement.Strategies;

public interface IMovementProcessor3D : IImpulseReceiver3D
{
    void ProcessMovement(IMovementStrategy3D strategy, Vector3 desiredDirection, float delta);
    void ProcessExternalForcesOnly(float delta);

    /// <summary>
    /// Settle-only physics tick for recovery states (WallHit / GroundFall) that must
    /// progress collision but not be re-affected by sustained environmental forces or
    /// velocity offsets. Applies pending impulses (one-shot) and runs Move(); skips
    /// the ExternalForceReceiver aggregate. Used to prevent wave-drag feedback loops
    /// during post-capture animation states.
    /// </summary>
    void ProcessImpulsesOnly(float delta);

    void ClearImpulses();

    /// <summary>
    /// The default movement strategy supplied at construction. Immutable. May be null
    /// for processors that always pass a strategy explicitly to ProcessMovement(strategy, ...).
    /// </summary>
    IMovementStrategy3D? Default { get; }

    /// <summary>
    /// The currently-active strategy: override slot if set, else Default. May be null
    /// if neither is set (caller should not invoke ProcessMovement(direction, delta) in
    /// that case).
    /// </summary>
    IMovementStrategy3D? ActiveStrategy { get; }

    /// <summary>
    /// Set the override strategy. Replaces any prior override. Idempotent on same instance.
    /// On conflict (different prior override) emits JmoLogger.Warning — the runtime tripwire
    /// for accidental cross-system overlap; the slot is single-writer-at-a-time by convention.
    /// </summary>
    void SetStrategyOverride(IMovementStrategy3D strategy);

    /// <summary>
    /// Clear the override slot. ActiveStrategy falls back to Default. No-op + Warning when
    /// slot is already empty.
    /// </summary>
    void ClearStrategyOverride();

    /// <summary>
    /// Tick the processor using ActiveStrategy. Resolves override-or-default internally.
    /// Throws InvalidOperationException if both override and Default are null.
    /// </summary>
    void ProcessMovement(Vector3 desiredDirection, float delta);

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
