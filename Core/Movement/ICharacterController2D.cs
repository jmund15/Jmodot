namespace Jmodot.Core.Movement;

using Shared;

/// <summary>
///     The definitive, low-level interface for directly manipulating a Godot 2D physics body.
///     It mirrors the 3D controller contract so shared gameplay systems can target either space
///     without being coupled to a concrete Godot node type.
/// </summary>
public interface ICharacterController2D : IGodotNodeInterface
{
    Vector2 GlobalPosition { get; }
    Vector2 Velocity { get; }

    void SetVelocity(Vector2 newVelocity);
    void AddVelocity(Vector2 additiveVelocity);
    void Move();
    void Teleport(Vector2 newGlobalPosition);
}
