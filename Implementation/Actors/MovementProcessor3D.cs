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
public class MovementProcessor3D : IMovementProcessor3D
{
    private readonly ICharacterController3D _controller;
    private readonly ExternalForceReceiver3D _forceReceiver3D;
    private readonly Node3D _owner;
    private readonly IStatProvider _stats;
    private readonly Attribute? _stabilityAttr;

    private Vector3 _frameImpulses = Vector3.Zero;

    public MovementProcessor3D(
        ICharacterController3D controller,
        IStatProvider statsProvider,
        ExternalForceReceiver3D forceReceiver3D,
        Node3D owner,
        Attribute? stabilityAttr = null)
    {
        this._controller = controller;
        this._stats = statsProvider;
        this._forceReceiver3D = forceReceiver3D;
        this._owner = owner;
        this._stabilityAttr = stabilityAttr;
    }

    /// <summary>
    ///     The main update loop for continuous movement. It is called by the active State,
    ///     which provides all necessary contextual information.
    /// </summary>
    public void ProcessMovement(IMovementStrategy3D strategy3D, Vector3 desiredDirection, float delta)
    {
        // --- 1. Calculate Character-Driven Velocity via the Strategy ---
        // The strategy does the heavy lifting of getting stats.
        var inputVelocity = this._controller.Velocity;
        var characterVelocity =
            strategy3D.CalculateVelocity(inputVelocity, desiredDirection, this._stats, delta);

        _controller.SetVelocity(characterVelocity);

        // --- 2. Apply Impulses (stored in velocity) ---
        _controller.AddVelocity(_frameImpulses);
        _frameImpulses = Vector3.Zero;

        // --- 3. Apply External Forces (stored - will be affected by friction next frame) ---
        ApplyExternalForces(delta);

        // --- 4. Get Velocity Offset (NOT stored - fresh each frame, friction-independent) ---
        var velocityOffset = ScaleByStability(_forceReceiver3D.GetTotalVelocityOffset(_owner));

        // --- 5. Execute the Final Move with offset ---
        _controller.AddVelocity(velocityOffset);
        _controller.Move();

        // --- 6. Remove offset so it's not affected by friction next frame ---
        _controller.AddVelocity(-velocityOffset);
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
        _frameImpulses = Vector3.Zero;

        // 2. Apply external forces
        this.ApplyExternalForces(delta);

        // 3. Get velocity offset (friction-independent)
        var velocityOffset = ScaleByStability(_forceReceiver3D.GetTotalVelocityOffset(_owner));

        // 4. Execute the move with offset
        _controller.AddVelocity(velocityOffset);
        this._controller.Move();

        // 5. Remove offset so it's not stored
        _controller.AddVelocity(-velocityOffset);
    }

    /// <summary>
    ///     Applies an instantaneous change in velocity to the character controller.
    ///     This is the primary method for all impulse-based mechanics.
    /// </summary>
    /// <param name="impulse">The velocity vector to add to the character's current velocity.</param>
    public void ApplyImpulse(Vector3 impulse)
    {
        _frameImpulses += impulse;
    }

    public void ClearImpulses()
    {
        _frameImpulses = Vector3.Zero;
    }

    private void ApplyExternalForces(float delta)
    {
        var externalForce = this._forceReceiver3D.GetTotalForce(this._owner);
        this._controller.AddVelocity(ScaleByStability(externalForce) * delta);
    }

    private Vector3 ScaleByStability(Vector3 force)
    {
        if (_stabilityAttr == null)
        {
            return force;
        }

        var stability = _stats.GetStatValue<float>(_stabilityAttr, 0f);
        return StabilityScaling.ScaleForce(force, stability);
    }
}
