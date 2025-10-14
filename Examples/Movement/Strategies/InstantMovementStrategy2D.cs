namespace Jmodot.Examples.Movement.Strategies;

using Core.Movement.Strategies;
using Core.Stats;
using Jmodot.Implementation.Movement.Strategies;
using Jmodot.Implementation.Registry;
using PushinPotions.Global;

[GlobalClass]
public partial class InstantMovementStrategy2D : BaseMovementStrategy2D, IMovementStrategy2D
{
    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection, IStatProvider stats, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(GlobalRegistry.DB.MaxSpeedAttr);
        return desiredDirection * maxSpeed * delta;
    }
}
