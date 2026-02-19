namespace Jmodot.Implementation.Movement;

using System;
using Core.Movement;

/// <summary>
///     An adapter that implements the ICharacterController3D interface
///     for a standard Godot CharacterBody3D node.
/// </summary>
public class CharacterBodyController3D : ICharacterController3D
{
    private readonly CharacterBody3D _body;

    public CharacterBodyController3D(CharacterBody3D body)
    {
        ArgumentNullException.ThrowIfNull(body);
        this._body = body;
    }

    public Node GetUnderlyingNode()
    {
        return this._body;
    }

    public Vector3 GlobalPosition => this._body.GlobalPosition;
    public Vector3 Velocity => this._body.Velocity;
    public bool IsOnFloor => this._body.IsOnFloor();
    public bool IsOnWall => this._body.IsOnWall();

    public Vector3 GetWallNormal() => this._body.GetWallNormal();

    public Vector3 PreMoveVelocity { get; private set; }
    public Vector3 LastNonZeroVelocity { get; private set; }

    public void SetVelocity(Vector3 newVelocity)
    {
        this._body.Velocity = newVelocity;
    }

    public void AddVelocity(Vector3 additiveVelocity)
    {
        this._body.Velocity += additiveVelocity;
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

    public void Teleport(Vector3 newGlobalPosition)
    {
        this._body.GlobalPosition = newGlobalPosition;
    }
}
