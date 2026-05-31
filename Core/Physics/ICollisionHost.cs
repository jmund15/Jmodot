namespace Jmodot.Core.Physics;

using Godot;
using Jmodot.Core.Health;
using Jmodot.Core.Identification;
using Jmodot.Core.Movement;
using Jmodot.Core.Shared;
using Jmodot.Implementation.Physics.Collision;

/// <summary>
/// The <em>kinematic</em> collision host — a body driven by an <see cref="ICharacterController3D"/>
/// (spells, ingredients, thrown objects). Specializes the body-agnostic
/// <see cref="ICollisionResponseHost"/> by supplying both response members from the controller:
/// - <see cref="ICollisionResponseHost.CollisionImpactVelocity"/> → the controller's pre-move velocity.
/// - <see cref="ICollisionResponseHost.EnactCollisionResponse"/> → runs the selected strategy against
///   this controller-backed host.
///
/// Both are default interface members, so existing kinematic implementers adopt the supertype with
/// no changes. Inherits <see cref="IDamageable"/> (self-damage; health-less hosts no-op TakeDamage),
/// <see cref="IIdentifiable"/> (category resolution), and <see cref="IGodotNodeInterface"/> (node
/// access for collision-exception wiring + <c>IImpactable</c> blackboard discovery) via the supertype.
/// </summary>
public interface ICollisionHost : ICollisionResponseHost
{
    /// <summary>
    /// The physics driver used to read pre-move velocity and write post-collision velocity.
    /// </summary>
    ICharacterController3D Controller { get; }

    Vector3 ICollisionResponseHost.CollisionImpactVelocity => Controller.PreMoveVelocity;

    PhysicsApplyResult ICollisionResponseHost.EnactCollisionResponse(
        ICollisionPhysicsStrategy? strategy, CollisionContact contact, float velocityRetention)
        => strategy?.Apply(this, contact, velocityRetention) ?? PhysicsApplyResult.Failed;
}
