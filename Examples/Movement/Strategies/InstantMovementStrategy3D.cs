namespace Jmodot.Examples.Movement.Strategies;

using Core.Movement.Strategies;
using Core.Stats;
using Jmodot.Implementation.Movement.Strategies;
using Jmodot.Implementation.Registry;

[GlobalClass]
public partial class InstantMovementStrategy3D : BaseMovementStrategy3D, IMovementStrategy3D
{
    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats,
        StatContext activeContext, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(GlobalRegistry.DB.MaxSpeedAttr, activeContext);
        var xzVec = desiredDirection * maxSpeed * delta;
        return new(xzVec.X, currentVelocity.Y, xzVec.Z);
    }
}
