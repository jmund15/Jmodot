namespace Jmodot.Implementation.Movement;

using Core.Movement;

/// <summary>
///     An adapter that implements the ICharacterController2D interface
///     for a standard Godot CharacterBody2D node.
/// </summary>
public class CharacterBodyController2D : ICharacterController2D
{
    private readonly CharacterBody2D _body;

    public CharacterBodyController2D(CharacterBody2D body)
    {
        _body = body;
    }

    public Node GetUnderlyingNode()
    {
        return _body;
    }

    public Vector2 GlobalPosition => _body.GlobalPosition;
    public Vector2 Velocity => _body.Velocity;
    public bool IsOnFloor => _body.IsOnFloor();

    public void SetVelocity(Vector2 newVelocity)
    {
        _body.Velocity = newVelocity;
    }

    public void AddVelocity(Vector2 additiveVelocity)
    {
        _body.Velocity += additiveVelocity;
    }

    public void Move()
    {
        _body.MoveAndSlide();
    }

    public void Teleport(Vector2 newGlobalPosition)
    {
        _body.GlobalPosition = newGlobalPosition;
    }
}
