namespace Jmodot.Core.Actors;

using Movement.Strategies;

public interface IMovementProcessor3D
{
    void ProcessMovement(IMovementStrategy3D strategy, Vector3 desiredDirection, float delta);
    void ProcessExternalForcesOnly(float delta);
    void ApplyImpulse(Vector3 impulse);
    void ClearImpulses();
}
