namespace Jmodot.Examples.Movement.Strategies;

using Core.Movement.Strategies;
using Core.Stats;
using Jmodot.Implementation.Movement.Strategies;
using Jmodot.Implementation.Registry;

[GlobalClass]
public partial class InstantMovementStrategy : BaseMovementStrategy, IMovementStrategy
{
    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats, MovementMode activeMode, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(GlobalRegistry.DB.MaxSpeedAttr, activeMode);
        var xzVec = desiredDirection * maxSpeed * delta;
        return new(xzVec.X, currentVelocity.Y, xzVec.Z);
    }
}
