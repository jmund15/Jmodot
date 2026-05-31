namespace Jmodot.Core.Physics;

using Godot;
using Jmodot.Core.Health;
using Jmodot.Core.Identification;
using Jmodot.Core.Shared;
using Jmodot.Implementation.Physics.Collision;

/// <summary>
/// The body-agnostic contract a host satisfies to delegate collision <em>response</em> to
/// <see cref="ICollisionResponder"/>. Segregates the responder's needs from the kinematic-only
/// <see cref="ICollisionHost"/>: the responder resolves a decision (category match, count,
/// thresholds, self-damage) against this surface alone, so a non-kinematic body — a RigidBody,
/// a ray-based beam — plugs in without faking an <c>ICharacterController3D</c>.
///
/// Two members the responder previously obtained via type-tests on the host:
/// - <see cref="CollisionImpactVelocity"/> replaces the pre-move velocity read.
/// - <see cref="EnactCollisionResponse"/> makes the physics-application step a polymorphic host
///   responsibility — a kinematic host runs the strategy; a rigid host enacts its own response.
///
/// <see cref="ICollisionHost"/> supplies both via default interface members, so existing
/// kinematic hosts adopt the supertype transparently.
///
/// 2D parity deferred (collision subsystem is 3D-only pending a subsystem-wide 2D pass).
/// </summary>
public interface ICollisionResponseHost : IDamageable, IIdentifiable, IGodotNodeInterface
{
    /// <summary>
    /// The impact velocity used for self-damage scaling and min-velocity / velocity-fallback
    /// thresholds. A kinematic host returns its pre-move velocity; a rigid host returns its own
    /// contact-time velocity source.
    /// </summary>
    Vector3 CollisionImpactVelocity { get; }

    /// <summary>
    /// Enacts the physics response for a resolved collision. The responder selects the strategy
    /// from its configured set and hands it in; the host decides how to apply it. A kinematic
    /// host delegates to <c>strategy.Apply(this, …)</c>; a rigid host enacts its own model and
    /// may ignore the strategy entirely (Null-Object).
    /// </summary>
    /// <returns>Applied (consume count + side effects), Skipped (persist, no side effects), or Failed (destroy).</returns>
    PhysicsApplyResult EnactCollisionResponse(ICollisionPhysicsStrategy? strategy, CollisionContact contact, float velocityRetention);
}
