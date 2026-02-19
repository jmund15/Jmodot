namespace Jmodot.Core.Movement;

using Shared;

/// <summary>
///     The definitive, low-level interface for applying physics forces to a RigidBody3D.
///     Commands correspond to applying forces and impulses within Godot's physics simulation.
/// </summary>
public interface IRigidController3D : IVelocityProvider3D, IGodotNodeInterface
{
    Vector3 GlobalPosition { get; }

    /// <summary>Applies a continuous force, affecting acceleration.</summary>
    void ApplyForce(Vector3 force);

    /// <summary>Applies an instantaneous change in velocity.</summary>
    void ApplyImpulse(Vector3 impulse);

    void Teleport(Vector3 newGlobalPosition);
}
