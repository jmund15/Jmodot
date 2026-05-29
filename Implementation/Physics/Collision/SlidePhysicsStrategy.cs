namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.Physics;
using Jmodot.Implementation.Combat;

/// <summary>
/// Slide physics strategy — dampens velocity to keep horizontal movement, zeroes vertical.
/// Used for entities that should persist along the ground surface.
/// </summary>
[GlobalClass, Tool]
public partial class SlidePhysicsStrategy : CollisionPhysicsStrategy
{
    public override PhysicsApplyResult Apply(ICollisionHost host, CollisionContact contact, float velocityRetention)
    {
        var controller = host.Controller;

        var velocity = controller.PreMoveVelocity;

        // Keep horizontal, zero vertical, apply retention
        var slideVelocity = new Vector3(
            velocity.X * velocityRetention,
            0f,
            velocity.Z * velocityRetention
        );

        controller.SetVelocity(slideVelocity);
        return PhysicsApplyResult.Applied;
    }

    public override void ConfigureBody(ICollisionHost host, HitboxComponent3D? hitbox)
    {
        // Slide doesn't need body configuration
    }
}
