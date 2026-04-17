namespace Jmodot.Implementation.Movement.Strategies;

using Core.Movement.Strategies;
using Core.Stats;

[GlobalClass]
public abstract partial class BaseMovementStrategy2D : Resource, IMovementStrategy2D
{
    public BaseMovementStrategy2D()
    {
    }

    public abstract Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection, IStatProvider stats,
        MovementMode activeMode, float delta);
}
