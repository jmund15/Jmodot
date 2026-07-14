namespace Jmodot.Core.Actors;

using Movement.Strategies;

public interface IMovementProcessor2D
{
    void ProcessMovement(IMovementStrategy2D strategy2D, Vector2 desiredDirection, float delta);

    /// <summary>
    /// Movement update with a cast-phase speed scale + amplified friction. <paramref name="speedScale"/>
    /// scales the strategy's character-driven velocity (0..1 = slow walk); <paramref name="frictionMultiplier"/>
    /// (&gt;=1) bleeds extra velocity each tick so accumulated impulses (e.g. cast recoil) decay
    /// faster than free locomotion. speedScale=1 + frictionMultiplier=1 reproduces the 3-arg overload.
    /// </summary>
    void ProcessMovement(IMovementStrategy2D strategy2D, Vector2 desiredDirection, float delta,
        float speedScale, float frictionMultiplier);

    void ProcessExternalForcesOnly(float delta);
    void ApplyImpulse(Vector2 impulse);
    void ClearImpulses();
}
