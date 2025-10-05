namespace Jmodot.Implementation.Movement.Strategies;

using Core.Stats;
using Core.Movement.Strategies;

[GlobalClass]
public abstract partial class BaseMovementStrategy3D : Resource, IMovementStrategy3D
{
    public BaseMovementStrategy3D() { }
    public abstract Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection,
        IStatProvider stats, StatContext activeContext, float delta);
}
