namespace Jmodot.Implementation.Physics.Collision;

using Jmodot.Core.Physics;
using Jmodot.Implementation.Combat;

/// <summary>
/// Strategy interface for applying physics responses to collisions.
/// Implementations are standalone Resources that can be shared across different DurableCollisionResponse configurations.
/// </summary>
public interface ICollisionPhysicsStrategy
{
    /// <summary>
    /// Apply the physics response to the host after a collision.
    /// </summary>
    /// <param name="host">The collision host entity.</param>
    /// <param name="contact">The contact data from the collision.</param>
    /// <param name="velocityRetention">Velocity multiplier to apply (from DurableCollisionResponse).</param>
    /// <returns>Applied if physics changed velocity, Skipped if persist without physics, Failed if destroy.</returns>
    PhysicsApplyResult Apply(ICollisionHost host, CollisionContact contact, float velocityRetention);

    /// <summary>
    /// Configure the host's physical body at initialization time (e.g., layer masks for pierce).
    /// </summary>
    /// <param name="host">The collision host entity.</param>
    /// <param name="hitbox">Optional hitbox for layer configuration (null for non-spell hosts).</param>
    void ConfigureBody(ICollisionHost host, HitboxComponent3D? hitbox);
}
