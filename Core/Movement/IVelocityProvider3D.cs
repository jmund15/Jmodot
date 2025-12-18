using Godot;

namespace Jmodot.Core.Movement;

/// <summary>
/// A centralized interface for any entity that has linear velocity.
/// Used by the Combat system (and others) to extract physics data without
/// needing to know the specific node implementation (CharacterBody vs RigidBody).
/// </summary>
public interface IVelocityProvider3D
{
    /// <summary>
    /// The current linear velocity of the object in Global Space.
    /// </summary>
    Vector3 LinearVelocity { get; }
}
