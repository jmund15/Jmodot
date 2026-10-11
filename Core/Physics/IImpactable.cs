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
    /// Resistance to being moved by a contact. Elastic contacts read inertia as Mass * (1 + Stability), the same
    /// split knockback uses: Stability resists, Mass sets how much speed a push buys.
    /// </summary>
    float Stability { get; }

    /// <summary>
    /// How heavy this entity is in elastic contacts, positive and finite. Below 1 it leaves a contact faster than
    /// an equal body would, up to (1 + restitution) times the closing speed; above 1 it moves less.
    /// </summary>
    float Mass => 1f;

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
    /// <param name="source">
    /// The node whose contact caused the impact. Implementations that attribute the contact to its
    /// source read it; the rest ignore it. Null when the caller has no source.
    /// </param>
    void ApplyImpactVelocity(Vector3 newVelocity, Node? source = null);

    /// <summary>
    /// How steeply this entity leaves an elastic contact, in degrees above its horizontal heading: the solve's
    /// horizontal speed is turned upward by this angle, so a struck body pops into the air. 0 keeps it level.
    /// </summary>
    float LaunchLiftDegrees => 0f;
}
