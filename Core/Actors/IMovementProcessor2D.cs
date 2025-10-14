namespace Jmodot.Core.Actors;

using Movement.Strategies;
using Stats;

public interface IMovementProcessor2D
{
    void ProcessMovement(IMovementStrategy2D strategy2D, MovementMode activeMode, Vector2 desiredDirection,
        float delta);
    void ProcessExternalForcesOnly(float delta);
    void ApplyImpulse(Vector2 impulse);
}
