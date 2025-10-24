namespace Jmodot.Examples.Movement.Strategies;

using Core.Movement.Strategies;
using Core.Stats;
using Jmodot.Implementation.Movement.Strategies;
using Jmodot.Implementation.Registry;
using PushinPotions.Global;

[GlobalClass, Tool]
public partial class InstantMovementStrategy3D : BaseMovementStrategy3D, IMovementStrategy3D
{
    // in this case, we're only affecting x & z, letting gravity and other forces impact y
    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(GlobalRegistry.DB.MaxSpeedAttr);
        var xzVec = desiredDirection * maxSpeed * delta;
        return new(xzVec.X, currentVelocity.Y, xzVec.Z);
    }
}
