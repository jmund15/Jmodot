namespace Jmodot.Implementation.Movement.Strategies;

using Core.Stats;
using Core.Movement.Strategies;

[GlobalClass, Tool]
public abstract partial class BaseMovementStrategy2D : Resource, IMovementStrategy2D
{
    public BaseMovementStrategy2D() { }
    public abstract Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        IStatProvider stats, float delta);
}
