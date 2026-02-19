namespace Jmodot.Implementation.Movement.Strategies;

using Core.Stats;

/// <summary>
/// A simple movement strategy for projectiles that passes through the desired direction unchanged.
/// The caller is responsible for pre-computing the direction and speed.
/// </summary>
[GlobalClass]
public partial class ProjectileStrategy : BaseMovementStrategy3D
{
    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats, float delta)
    {
        return desiredDirection;
    }
}
