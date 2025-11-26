namespace Jmodot.Implementation.Movement.Strategies;

using Actors;
using Core.Movement.Strategies;
using Core.Stats;
using Jmodot.Core.Movement;
using Godot;

/// <summary>
/// A simple movement strategy for projectiles that just move forward based on their current velocity.
/// </summary>
public class ProjectileStrategy : IMovementStrategy3D
{
    public Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats, float delta)
    {
        // Projectiles typically just keep their velocity
        // If we wanted to enforce a constant speed, we could do it here.

        return desiredDirection;
    }
}
