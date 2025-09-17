#region

using Jmodot.Core.Movement;
using Jmodot.Core.Movement.Strategies;
using Jmodot.Core.Stats;

#endregion

namespace Jmodot.Implementation.Actors;

/// <summary>
///     The definitive high-level orchestrator for character movement. Its sole responsibility
///     is to be a pure calculation engine. It takes the active strategy, the character's final
///     modified stats, and a pre-calculated desired direction, and uses them to calculate the
///     final velocity command for the IMovementController. It is a reusable, stateless service
///     called by the character's State Machine.
/// </summary>
public class MovementProcessor
{
    private readonly ICharacterController3D _controller;
    private readonly ExternalForceReceiver _forceReceiver;
    private readonly Node3D _owner;
    private readonly IStatProvider _stats; // Now it needs a reference to the StatController

    //private readonly Vector3 _gravity = Vector3.Down * 9.8f;

    public MovementProcessor(
        ICharacterController3D controller,
        IStatProvider statsProvider,
        ExternalForceReceiver forceReceiver,
        Node3D owner)
    {
        _controller = controller;
        _stats = statsProvider;
        _forceReceiver = forceReceiver;
        _owner = owner;
    }

    /// <summary>
    ///     The main update loop for continuous movement. It is called by the active State,
    ///     which provides all necessary contextual information.
    /// </summary>
    public void ProcessMovement(IMovementStrategy strategy, MovementMode activeMode, Vector3 desiredDirection,
        float delta)
    {
        // --- 1. Calculate Character-Driven Velocity via the Strategy ---
        // The strategy does the heavy lifting of getting stats.
        var characterVelocity =
            strategy.CalculateVelocity(_controller.Velocity, desiredDirection, _stats, activeMode, delta);
        _controller.SetVelocity(characterVelocity); // The strategy now returns the full vector including Y

        // --- 2. Apply External Forces (Gravity, Environment) ---
        ApplyExternalForces(delta);

        // --- 3. Execute the Final Move ---
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
        ApplyExternalForces(delta);

        // 3. Execute the move
        _controller.Move();
    }

    /// <summary>
    ///     Applies an instantaneous change in velocity to the character controller.
    ///     This is the primary method for all impulse-based mechanics.
    /// </summary>
    /// <param name="impulse">The velocity vector to add to the character's current velocity.</param>
    public void ApplyImpulse(Vector3 impulse)
    {
        _controller.AddVelocity(impulse);
    }

    private void ApplyExternalForces(float delta)
    {
        if (!_controller.IsOnFloor)
        {
            // A better way to get gravity settings, still bad, should be used by ForceReceiver too.
            var gravityVec = ProjectSettings.GetSetting("physics/3d/default_gravity_vector").AsVector3();
            var gravityMag = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
            _controller.AddVelocity(gravityVec * gravityMag * delta);
        }

        // TODO: this force receiver should also handle gravity, instead of being hardcoded above.
        var externalForce = _forceReceiver.GetTotalForce(_owner);
        _controller.AddVelocity(externalForce * delta);
    }
}