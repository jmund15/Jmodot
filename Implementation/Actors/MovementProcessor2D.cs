namespace Jmodot.Implementation.Actors;

using Core.Movement;
using Core.Movement.Strategies;
using Core.Stats;

/// <summary>
///     The 2D counterpart to MovementProcessor. It is a small, stateless orchestration layer
///     that lets a state machine or gameplay controller reuse movement strategies without being
///     coupled directly to a concrete CharacterBody2D.
/// </summary>
public class MovementProcessor2D
{
    private readonly ICharacterController2D _controller;
    private readonly IStatProvider _stats;
    private Vector2 _frameImpulses = Vector2.Zero;

    public MovementProcessor2D(ICharacterController2D controller, IStatProvider statsProvider)
    {
        this._controller = controller;
        this._stats = statsProvider;
    }

    public void ProcessMovement(IMovementStrategy2D strategy, MovementMode activeMode, Vector2 desiredDirection,
        float delta)
    {
        var characterVelocity =
            strategy.CalculateVelocity(this._controller.Velocity, desiredDirection, this._stats, activeMode, delta);
        this._controller.SetVelocity(characterVelocity);

        if (!this._frameImpulses.IsZeroApprox())
        {
            this._controller.AddVelocity(this._frameImpulses);
            this._frameImpulses = Vector2.Zero;
        }

        this._controller.Move();
    }

    public void ProcessImpulsesOnly()
    {
        if (!this._frameImpulses.IsZeroApprox())
        {
            this._controller.AddVelocity(this._frameImpulses);
            this._frameImpulses = Vector2.Zero;
        }

        this._controller.Move();
    }

    public void ApplyImpulse(Vector2 impulse)
    {
        this._frameImpulses += impulse;
    }
}
