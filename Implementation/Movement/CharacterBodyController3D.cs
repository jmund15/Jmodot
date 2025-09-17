namespace Jmodot.Implementation.Movement;

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
        this._body = body;
    }

    public Node GetInterfaceNode()
    {
        return this._body;
    }

    public Vector3 GlobalPosition => this._body.GlobalPosition;
    public Vector3 Velocity => this._body.Velocity;
    public bool IsOnFloor => this._body.IsOnFloor();

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
        this._body.MoveAndSlide();
    }

    public void Teleport(Vector3 newGlobalPosition)
    {
        this._body.GlobalPosition = newGlobalPosition;
    }
}
