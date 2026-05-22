namespace Jmodot.Implementation.Interaction;

using System;
using Godot;
using Jmodot.Core.Interaction;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Movement.Strategies;
using Jmodot.Implementation.Shared;

/// <summary>
/// Drives a parent <see cref="CharacterBody3D"/> as a thrown projectile. On <see cref="Throw"/>
/// it integrates flight through <see cref="FlightStrategy"/> each physics frame and dispatches
/// the optional <see cref="Impact"/> behavior on the first collision. Self-contained — needs no
/// HSM state. Mirrors <c>GrabbableComponent3D</c>'s child-component-manipulates-parent-body pattern.
/// </summary>
[GlobalClass]
public partial class ThrowableComponent3D : Node3D, IThrowable3D
{
    [Export, RequiredExport] public BaseMovementStrategy3D FlightStrategy { get; set; } = null!;

    /// <summary>Behavior dispatched on first impact. Null = inert projectile (no impact effect).</summary>
    [Export] public ThrowImpactBehavior? Impact { get; set; }

    public bool IsFlying { get; private set; }
    public event Action<Node3D, Vector3> OnThrown = delegate { };

    private CharacterBody3D _body = null!;
    private Vector3 _velocity;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        _body = GetParent<CharacterBody3D>();
        if (_body == null)
        {
            JmoLogger.Error(this, "ThrowableComponent3D must be a child of a CharacterBody3D to drive flight.");
        }
        SetPhysicsProcess(false);
    }

    public void Throw(Vector3 throwVelocity)
    {
        _velocity = throwVelocity;
        IsFlying = true;
        SetPhysicsProcess(true);
        OnThrown?.Invoke(_body, throwVelocity);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsFlying || _body == null) { return; }

        // Projectile flight strategies consume desiredDirection as a pre-scaled velocity and are
        // stats-agnostic (see physics_patterns "Movement Strategy Selection").
        _velocity = FlightStrategy.CalculateVelocity(_velocity, _velocity, _velocity, null!, (float)delta);
        _body.Velocity = _velocity;
        _body.MoveAndSlide();

        if (_body.GetSlideCollisionCount() > 0)
        {
            var hit = _body.GetSlideCollision(0).GetCollider() as Node3D;
            Impact?.OnImpact(_body, hit, _velocity);
            IsFlying = false;
            SetPhysicsProcess(false);
        }
    }
}
