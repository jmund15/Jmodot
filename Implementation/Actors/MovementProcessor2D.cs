namespace Jmodot.Implementation.Actors;

using Core.Actors;
using Core.Movement;
using Core.Movement.Strategies;
using Core.Stats;

/// <summary>
///     The definitive high-level orchestrator for character movement. Its sole responsibility
///     is to be a pure calculation engine. It takes the active strategy, the character's final
///     modified stats, and a pre-calculated desired direction, and uses them to calculate the
///     final velocity command for the IMovementController. It is a reusable, stateless service
///     called by the character's State Machine.
/// </summary>
public class MovementProcessor2D : IMovementProcessor2D
{
    private readonly ICharacterController2D _controller;
    private readonly ExternalForceReceiver2D _forceReceiver2D;
    private readonly Node2D _owner;
    private readonly IStatProvider _stats;

    private Vector2 _frameImpulses = Vector2.Zero;

    public MovementProcessor2D(
        ICharacterController2D controller,
        IStatProvider statsProvider,
        ExternalForceReceiver2D forceReceiver2D,
        Node2D owner)
    {
        this._controller = controller;
        this._stats = statsProvider;
        this._forceReceiver2D = forceReceiver2D;
        this._owner = owner;
    }

    /// <summary>
    ///     The main update loop for continuous movement. It is called by the active State,
    ///     which provides all necessary contextual information.
    /// </summary>
    public void ProcessMovement(IMovementStrategy2D strategy2D, Vector2 desiredDirection, float delta)
    {
        // --- 1. Calculate Character-Driven Velocity via the Strategy ---
        var characterVelocity =
            strategy2D.CalculateVelocity(this._controller.Velocity, desiredDirection, _stats, delta);
        _controller.SetVelocity(characterVelocity);

        // --- 2. Apply Impulses (stored in velocity) ---
        _controller.AddVelocity(_frameImpulses);
        _frameImpulses = Vector2.Zero;

        // --- 3. Apply External Forces (stored - will be affected by friction next frame) ---
        ApplyExternalForces(delta);

        // --- 4. Get Velocity Offset (NOT stored - fresh each frame, friction-independent) ---
        var velocityOffset = _forceReceiver2D.GetTotalVelocityOffset(_owner);

        // --- 5. Execute the Final Move with offset ---
        var baseVelocity = _controller.Velocity;
        var combined = baseVelocity + velocityOffset;
        _controller.SetVelocity(combined);
        _controller.Move();

        // --- 6. Isolate collision delta and apply to base velocity only ---
        var postCollision = _controller.Velocity;
        var collisionDelta = postCollision - combined;
        _controller.SetVelocity(baseVelocity + collisionDelta);
    }

    /// <summary>
    ///     An update loop for states where the character is passive (e.g., stunned, interacting).
    ///     It does not run a movement strategy but still applies gravity and other external forces.
    /// </summary>
    public void ProcessExternalForcesOnly(float delta)
    {
        // No strategy is run. We respect the velocity set by other systems (e.g., knockback impulse).
        // 1. Still apply any impulses that might occur
        _controller.AddVelocity(_frameImpulses);
        _frameImpulses = Vector2.Zero;

        // 2. Apply external forces
        this.ApplyExternalForces(delta);

        // 3. Get velocity offset (friction-independent)
        var velocityOffset = _forceReceiver2D.GetTotalVelocityOffset(_owner);

        // 4. Execute the move with offset, isolating collision effects
        var baseVelocity = _controller.Velocity;
        var combined = baseVelocity + velocityOffset;
        _controller.SetVelocity(combined);
        _controller.Move();

        // 5. Apply collision delta to base velocity only
        var postCollision = _controller.Velocity;
        var collisionDelta = postCollision - combined;
        _controller.SetVelocity(baseVelocity + collisionDelta);
    }

    /// <summary>
    ///     Applies an instantaneous change in velocity to the character controller.
    ///     This is the primary method for all impulse-based mechanics.
    /// </summary>
    /// <param name="impulse">The velocity vector to add to the character's current velocity.</param>
    public void ApplyImpulse(Vector2 impulse)
    {
        _frameImpulses += impulse;
    }

    /// <summary>
    ///     Discards any pending impulses that have not yet been applied.
    ///     Useful when transitioning between states that should not carry over queued impulses.
    /// </summary>
    public void ClearImpulses()
    {
        _frameImpulses = Vector2.Zero;
    }

    private void ApplyExternalForces(float delta)
    {
        // TODO: this force receiver should also handle gravity, instead of being hardcoded above.
        var externalForce = this._forceReceiver2D.GetTotalForce(this._owner);
        this._controller.AddVelocity(externalForce * delta);
    }
}
