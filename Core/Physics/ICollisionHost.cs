namespace Jmodot.Core.Physics;

using Jmodot.Core.Health;
using Jmodot.Core.Identification;
using Jmodot.Core.Movement;
using Jmodot.Core.Shared;

/// <summary>
/// Minimal contract a framework body must satisfy to delegate collision response to
/// <see cref="ICollisionResponder"/> (spells, ingredients, thrown objects, etc.).
///
/// Composes the capabilities the responder and its physics strategies require:
/// - <see cref="Controller"/> (<see cref="ICharacterController3D"/>): pre-move velocity read +
///   post-collision velocity write for bounce / slide / pierce.
/// - <see cref="IDamageable"/>: self-damage-on-impact. A health-less host supplies an
///   implementation whose <see cref="IDamageable.TakeDamage"/> no-ops, preserving the
///   "no health → no self-damage side effects" contract.
/// - <see cref="IIdentifiable"/>: category resolution against the contact's collider.
/// - <see cref="IGodotNodeInterface"/>: underlying node for collision-exception wiring and
///   <c>IImpactable</c> discovery via the host's blackboard.
/// </summary>
public interface ICollisionHost : IVelocityProvider3D, IDamageable, IIdentifiable, IGodotNodeInterface
{
    /// <summary>
    /// The physics driver used to read pre-move velocity and write post-collision velocity.
    /// </summary>
    ICharacterController3D Controller { get; }
}
