namespace Jmodot.Implementation.Movement;

using System;
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
        ArgumentNullException.ThrowIfNull(body);
        _body = body;
    }

    public Node GetUnderlyingNode()
    {
        return _body;
    }

    public Vector2 GlobalPosition => _body.GlobalPosition;
    public Vector2 Velocity => _body.Velocity;
    public bool IsOnFloor => _body.IsOnFloor();
    public bool IsOnWall => _body.IsOnWall();

    public Vector2 GetWallNormal() => _body.GetWallNormal();

    public Vector2 PreMoveVelocity { get; private set; }
    public Vector2 LastNonZeroVelocity { get; private set; }

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
        PreMoveVelocity = _body.Velocity;
        _body.MoveAndSlide();
        if (_body.Velocity.LengthSquared() > 1e-6f)
        {
            LastNonZeroVelocity = _body.Velocity;
        }
    }

    public void Teleport(Vector2 newGlobalPosition)
    {
        _body.GlobalPosition = newGlobalPosition;
    }
}
