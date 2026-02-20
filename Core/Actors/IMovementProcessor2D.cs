namespace Jmodot.Core.Actors;

using Movement.Strategies;

public interface IMovementProcessor2D
{
    void ProcessMovement(IMovementStrategy2D strategy2D, Vector2 desiredDirection, float delta);
    void ProcessExternalForcesOnly(float delta);
    void ApplyImpulse(Vector2 impulse);
    void ClearImpulses();
}
