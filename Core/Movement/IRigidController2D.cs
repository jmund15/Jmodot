namespace Jmodot.Core.Movement;

using Shared;

/// <summary>
///     The definitive, low-level interface for applying physics forces to a RigidBody2D.
///     Commands correspond to applying forces and impulses within Godot's physics simulation.
/// </summary>
public interface IRigidController2D : IGodotNodeInterface
{
    Vector2 GlobalPosition { get; }
    Vector2 LinearVelocity { get; } // Read-only for rigid bodies

    /// <summary>Applies a continuous force, affecting acceleration.</summary>
    void ApplyForce(Vector2 force);

    /// <summary>Applies an instantaneous change in velocity.</summary>
    void ApplyImpulse(Vector2 impulse);

    void Teleport(Vector2 newGlobalPosition);
}
