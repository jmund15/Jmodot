namespace Jmodot.Implementation.Interaction;

using System;
using Godot;
using Jmodot.Core.Interaction;
using Jmodot.Implementation.Shared;

/// <summary>
/// Releases a parent <see cref="CharacterBody3D"/> to fall and settle at rest. On <see cref="Drop"/>
/// it zeroes horizontal velocity and lets gravity settle the body, raising <see cref="OnDropped"/>.
/// Self-contained — needs no HSM state. Mirrors <c>GrabbableComponent3D</c>'s
/// child-component-manipulates-parent-body pattern.
/// </summary>
[GlobalClass]
public partial class DroppableComponent3D : Node3D, IDroppable3D
{
    /// <summary>Downward settle speed applied on drop (units/sec). Gravity is engine-applied via MoveAndSlide.</summary>
    [Export] public float SettleGravity { get; set; } = 9.8f;

    public bool IsSettled { get; private set; } = true;
    public event Action<Node3D> OnDropped = delegate { };

    private CharacterBody3D _body = null!;

    public override void _Ready()
    {
        _body = GetParent<CharacterBody3D>();
        if (_body == null)
        {
            JmoLogger.Error(this, "DroppableComponent3D must be a child of a CharacterBody3D to settle.");
        }
        SetPhysicsProcess(false);
    }

    public void Drop()
    {
        if (_body == null) { return; }
        IsSettled = false;
        _body.Velocity = new Vector3(0f, _body.Velocity.Y, 0f);
        SetPhysicsProcess(true);
        OnDropped?.Invoke(_body);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsSettled || _body == null) { return; }

        var v = _body.Velocity;
        v.X = 0f;
        v.Z = 0f;
        v.Y = _body.IsOnFloor() ? 0f : v.Y - SettleGravity * (float)delta;
        _body.Velocity = v;
        _body.MoveAndSlide();

        if (_body.IsOnFloor())
        {
            IsSettled = true;
            SetPhysicsProcess(false);
        }
    }
}
