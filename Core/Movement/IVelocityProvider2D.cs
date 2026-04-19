using Godot;

namespace Jmodot.Core.Movement;

/// <summary>
/// A centralized interface for any entity that has linear velocity (2D).
/// Used by the Combat system (and others) to extract physics data without
/// needing to know the specific node implementation (CharacterBody2D vs RigidBody2D).
/// Dimension-parallel sibling: <see cref="IVelocityProvider3D"/>.
/// </summary>
public interface IVelocityProvider2D
{
    /// <summary>
    /// The current linear velocity of the object in Global Space.
    /// </summary>
    Vector2 LinearVelocity { get; }
}
