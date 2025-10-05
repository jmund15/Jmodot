namespace Jmodot.Examples.Movement.Strategies;

using Core.Movement.Strategies;
using Core.Stats;
using Jmodot.Implementation.Movement.Strategies;
using Jmodot.Implementation.Registry;

[GlobalClass]
public partial class InstantMovementStrategy2D : BaseMovementStrategy2D, IMovementStrategy2D
{
    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection, IStatProvider stats,
        StatContext activeContext, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(GlobalRegistry.DB.MaxSpeedAttr, activeContext);
        return desiredDirection * maxSpeed * delta;
    }
}
