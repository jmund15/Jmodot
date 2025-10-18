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
    private readonly IStatProvider _stats; // Now it needs a reference to the StatController

    //private readonly Vector2 _gravity = Vector2.Down * 9.8f;
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

        var gravityVec = ProjectSettings.GetSetting("physics/2d/default_gravity_vector").AsVector2();
        var gravityMag = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
        GD.Print($"gravity vec: {gravityVec}\ngravity mag: {gravityMag}");
    }

    /// <summary>
    ///     The main update loop for continuous movement. It is called by the active State,
    ///     which provides all necessary contextual information.
    /// </summary>
    public void ProcessMovement(IMovementStrategy2D strategy2D, Vector2 desiredDirection, float delta)
    {
        // --- 1. Calculate Character-Driven Velocity via the Strategy ---
        // The strategy does the heavy lifting of getting stats.
        var characterVelocity =
            strategy2D.CalculateVelocity(this._controller.Velocity, desiredDirection, _stats, delta);
        _controller.SetVelocity(characterVelocity
            ); // TODO: FIXXXXXXX, should strategy be in charge of handling jump/y velocity?


        // TODO: currently adding to keep 'ApplyImpulse' functionality, but should probably be set and add impulses after
        //GD.Print($"moving with vec: {characterVelocity}");

        // --- 2. Apply Impulses
        _controller.AddVelocity(_frameImpulses);
        _frameImpulses = Vector2.Zero; // reset after applied

        // --- 2. Apply External Forces (Gravity, Environment) ---
        ApplyExternalForces(delta);

        // --- 4. Execute the Final Move ---
        _controller.Move();
    }

    /// <summary>
    ///     An update loop for states where the character is passive (e.g., stunned, interacting).
    ///     It does not run a movement strategy but still applies gravity and other external forces.
    /// </summary>
    public void ProcessExternalForcesOnly(float delta)
    {
        // 1. No strategy is run. We respect the velocity set by other systems (e.g., knockback impulse).
        // Still apply any impulses that might occur
        _controller.AddVelocity(_frameImpulses);
        _frameImpulses = Vector2.Zero;

        // 2. Apply external forces
        this.ApplyExternalForces(delta);

        // 2. Execute the move
        this._controller.Move();
    }

    /// <summary>
    ///     Applies an instantaneous change in velocity to the character controller.
    ///     This is the primary method for all impulse-based mechanics.
    /// </summary>
    /// <param name="impulse">The velocity vector to add to the character's current velocity.</param>
    public void ApplyImpulse(Vector2 impulse)
    {
        _frameImpulses += impulse;
        //this._controller.AddVelocity(impulse);
    }

    private void ApplyExternalForces(float delta)
    {
        // TODO: this force receiver should also handle gravity, instead of being hardcoded above.
        var externalForce = this._forceReceiver2D.GetTotalForce(this._owner);
        this._controller.AddVelocity(externalForce * delta);
    }
}
