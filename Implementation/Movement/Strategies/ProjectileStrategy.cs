namespace Jmodot.Implementation.Movement.Strategies;

using Core.Stats;

/// <summary>
/// A simple movement strategy for projectiles that passes through the desired direction unchanged.
/// <para>
/// <b>Contract note:</b> Unlike other strategies where <c>desiredDirection</c> is a normalized unit vector,
/// this strategy expects <c>desiredDirection</c> to carry the full velocity (direction * speed).
/// The returned value IS the new velocity — no stat lookups or scaling are performed.
/// </para>
/// </summary>
[GlobalClass]
public partial class ProjectileStrategy : BaseMovementStrategy3D
{
    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats, float delta)
    {
        return desiredDirection;
    }
}
