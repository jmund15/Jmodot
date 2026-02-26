namespace Jmodot.Core.Physics;

using Godot;
using Jmodot.Core.Shared;

/// <summary>
/// Interface for entities that participate in elastic collision resolution.
/// Applicable to gameplay entities (PhysicsInteractionComponent) and
/// future surface materials (e.g., ice wall, rubber wall).
/// The collision formula treats all implementors uniformly — a wall is just
/// an entity with infinite stability and zero velocity.
/// </summary>
public interface IImpactable : IGodotNodeInterface
{
    /// <summary>Whether this entity participates in two-body elastic collision resolution.</summary>
    bool ParticipatesInElasticCollisions { get; }

    /// <summary>
    /// Stability value used as mass in elastic collisions: mass = 1 + Stability.
    /// Higher stability = heavier = harder to move.
    /// </summary>
    float Stability { get; }

    /// <summary>Current velocity of this entity (for relative velocity calculation).</summary>
    Vector3 Velocity { get; }

    /// <summary>
    /// Entity's material bounciness for entity-entity collisions.
    /// Combined with the other entity's restitution via geometric mean: sqrt(a * b).
    /// Range 0..1: 0 = perfectly inelastic (thud), 1 = perfectly elastic (billiard ball).
    /// </summary>
    float BounceRestitution { get; }

    /// <summary>
    /// Apply the resolved post-collision velocity to this entity.
    /// Implementation should compute the delta and apply as impulse to preserve other forces.
    /// </summary>
    void ApplyImpactVelocity(Vector3 newVelocity);
}
