namespace Jmodot.Implementation.Actors;

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
public class MovementProcessor3D
{
    private readonly ICharacterController3D _controller;
    private readonly ExternalForceReceiver3D _forceReceiver3D;
    private readonly Node3D _owner;
    private readonly IStatProvider _stats; // Now it needs a reference to the StatController

    //private readonly Vector3 _gravity = Vector3.Down * 9.8f;
    private Vector3 _frameImpulses = Vector3.Zero;

    public MovementProcessor3D(
        ICharacterController3D controller,
        IStatProvider statsProvider,
        ExternalForceReceiver3D forceReceiver3D,
        Node3D owner)
    {
        this._controller = controller;
        this._stats = statsProvider;
        this._forceReceiver3D = forceReceiver3D;
        this._owner = owner;

        var gravityVec = ProjectSettings.GetSetting("physics/3d/default_gravity_vector").AsVector3();
        var gravityMag = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
    }

    /// <summary>
    ///     The main update loop for continuous movement. It is called by the active State,
    ///     which provides all necessary contextual information.
    /// </summary>
    public void ProcessMovement(IMovementStrategy3D strategy3D, Vector3 desiredDirection,
        float delta)
    {
        // --- 1. Calculate Character-Driven Velocity via the Strategy ---
        // The strategy does the heavy lifting of getting stats.
        var characterVelocity =
            strategy3D.CalculateVelocity(this._controller.Velocity, desiredDirection, this._stats, delta);
        _controller.SetVelocity(characterVelocity
            ); // TODO: FIXXXXXXX, should strategy be in charge of handling jump/y velocity?

        // The strategy now returns the full vector including Y

        // TODO: currently adding to keep 'ApplyImpulse' functionality, but should probably be set and add impulses after
        //GD.Print($"moving with vec: {characterVelocity}");

        // --- 2. Apply Impulses
        _controller.AddVelocity(_frameImpulses);
        _frameImpulses = Vector3.Zero; // reset after applied

        // --- 3. Apply External Forces (Gravity, Environment) ---
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

        // 2. Apply external forces
        this.ApplyExternalForces(delta);

        // 3. Execute the move
        this._controller.Move();
    }

    /// <summary>
    ///     Applies an instantaneous change in velocity to the character controller.
    ///     This is the primary method for all impulse-based mechanics.
    /// </summary>
    /// <param name="impulse">The velocity vector to add to the character's current velocity.</param>
    public void ApplyImpulse(Vector3 impulse)
    {
        _frameImpulses += impulse;
        //this._controller.AddVelocity(impulse);
    }

    private void ApplyExternalForces(float delta)
    {
        if (!this._controller.IsOnFloor)
        {
            // A better way to get gravity settings.
            // TODO: still bad, should be used by ForceReceiver too.
            var gravityVec = ProjectSettings.GetSetting("physics/3d/default_gravity_vector").AsVector3();
            var gravityMag = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
            //GD.Print($"gravity vec: {gravityVec * gravityMag * delta}");
            _controller.AddVelocity(gravityVec * gravityMag * delta * 5f);
        }

        // TODO: this force receiver should also handle gravity, instead of being hardcoded above.
        var externalForce = this._forceReceiver3D.GetTotalForce(this._owner);
        this._controller.AddVelocity(externalForce * delta);
    }
}
