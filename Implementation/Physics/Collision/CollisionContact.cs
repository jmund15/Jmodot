namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.Identification;

/// <summary>
/// Entity-agnostic collision contact data. Abstracts away the differences between
/// CharacterBody3D (KinematicCollision3D) and RigidBody3D (PhysicsDirectBodyState3D).
///
/// Host-independent: carries a <see cref="StringName"/> <see cref="Reason"/> whose
/// vocabulary is owned by the consuming project. Construction from a concrete
/// physics collision (and any project-specific reason/identity resolution) lives
/// in the consumer's own factory layer.
/// </summary>
public struct CollisionContact
{
    /// <summary>Framework-neutral default reason used when a caller supplies none.</summary>
    public static readonly StringName DefaultReason = "Generic";

    /// <summary>
    /// The global position of the contact point.
    /// </summary>
    public Vector3 Position;

    /// <summary>
    /// The normal vector of the surface at the contact point.
    /// </summary>
    public Vector3 Normal;

    /// <summary>
    /// The physics body or node that was hit.
    /// </summary>
    public Node Collider;

    /// <summary>
    /// The specific reason for the hit (Ground, Wall, Entity, etc.)
    /// </summary>
    public StringName Reason;

    /// <summary>
    /// The semantic identity of the collider, if it has one.
    /// </summary>
    public Identity? Identity;

    /// <summary>
    /// The linear velocity of the collider at the contact point, if available.
    /// Captured from KinematicCollision3D.GetColliderVelocity() (CharacterBody path)
    /// or from RigidBody3D.LinearVelocity / IVelocityProvider3D.LinearVelocity
    /// (RigidBody / generic paths). Vector3.Zero for static colliders.
    ///
    /// Used by passive mode's collision interceptor to apply push impulse
    /// proportional to the kicker's velocity.
    /// </summary>
    public Vector3 ColliderVelocity;

    public CollisionContact(Vector3 position, Vector3 normal, Node collider, StringName? reason = null, Identity? identity = null, Vector3 colliderVelocity = default)
    {
        Position = position;
        Normal = normal;
        Collider = collider;
        Reason = reason ?? DefaultReason;
        Identity = identity;
        ColliderVelocity = colliderVelocity;
    }
}
