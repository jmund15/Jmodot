namespace Jmodot.Core.Actors;

using Movement.Strategies;
using Stats;

public interface IMovementProcessor3D
{
    void ProcessMovement(IMovementStrategy3D strategy2D, Vector3 desiredDirection, float delta);
    void ProcessExternalForcesOnly(float delta);
    void ApplyImpulse(Vector3 impulse);
}
