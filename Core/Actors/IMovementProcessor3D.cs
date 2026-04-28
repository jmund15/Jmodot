namespace Jmodot.Core.Actors;

using Movement.Strategies;

public interface IMovementProcessor3D
{
    void ProcessMovement(IMovementStrategy3D strategy, Vector3 desiredDirection, float delta);
    void ProcessExternalForcesOnly(float delta);

    /// <summary>
    /// Settle-only physics tick for recovery states (WallHit / GroundFall) that must
    /// progress collision but not be re-affected by sustained environmental forces or
    /// velocity offsets. Applies pending impulses (one-shot) and runs Move(); skips
    /// the ExternalForceReceiver aggregate. Used to prevent wave-drag feedback loops
    /// during post-capture animation states.
    /// </summary>
    void ProcessImpulsesOnly(float delta);

    void ApplyImpulse(Vector3 impulse);
    void ClearImpulses();
}
