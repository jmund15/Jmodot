namespace Jmodot.Implementation.Movement.Strategies;

using Core.Stats;
using Core.Movement.Strategies;

[GlobalClass]
public abstract partial class BaseMovementStrategy : Resource, IMovementStrategy
{
    public BaseMovementStrategy() { }
    public abstract Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats, MovementMode activeMode, float delta);
}
