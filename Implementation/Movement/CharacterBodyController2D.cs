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
        this._body = body;
    }

    public Node GetUnderlyingNode()
    {
        return this._body;
    }

    public Vector2 GlobalPosition => this._body.GlobalPosition;
    public Vector2 Velocity => this._body.Velocity;
    public bool IsOnFloor => this._body.IsOnFloor();

    public void SetVelocity(Vector2 newVelocity)
    {
        this._body.Velocity = newVelocity;
    }

    public void AddVelocity(Vector2 additiveVelocity)
    {
        this._body.Velocity += additiveVelocity;
    }

    public void Move()
    {
        this._body.MoveAndSlide();
    }

    public void Teleport(Vector2 newGlobalPosition)
    {
        this._body.GlobalPosition = newGlobalPosition;
    }
}
