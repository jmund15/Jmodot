namespace Jmodot.Core.Actors;

using Movement.Strategies;

public interface IMovementProcessor3D
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

    void ApplyImpulse(Vector3 impulse);
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
}
