namespace Jmodot.Implementation.Movement.Forces;

using Godot;
using Core.Environment;
using Actors;
using Shared;
using Shared.GodotExceptions;

/// <summary>
/// A reusable force provider that applies gravity to ground-based CharacterBody3D entities
/// via the ExternalForceReceiver3D pipeline. Returns CharacterBody3D.GetGravity() when the
/// body is not on the floor, and Vector3.Zero when grounded.
///
/// Scene tree expects:
///   CharacterBody3D
///   └── ExternalForceReceiver3D (Area3D)
///       └── GravityForceProvider3D (this node)
/// </summary>
[GlobalClass]
public partial class GravityForceProvider3D : Node, IForceProvider3D
{
    private CharacterBody3D _body = null!;

    public override void _Ready()
    {
        // Walk up: this → ExternalForceReceiver3D → CharacterBody3D
        var receiver = GetParent() as ExternalForceReceiver3D
            ?? throw new NodeConfigurationException(
                "GravityForceProvider3D must be a direct child of ExternalForceReceiver3D.", this);

        var parent = receiver.GetParent();
        _body = parent as CharacterBody3D
            ?? throw new NodeConfigurationException(
                "GravityForceProvider3D requires a CharacterBody3D grandparent.", this);

        receiver.RegisterInternalProvider(this);
    }

    public Vector3 GetForceFor(Node3D target)
    {
        return IsGrounded() ? Vector3.Zero : GetGravityVector();
    }

    protected virtual bool IsGrounded() => _body.IsOnFloor();
    protected virtual Vector3 GetGravityVector() => _body.GetGravity();

    #region Test Helpers
#if TOOLS
    internal void SetBody(CharacterBody3D body) => _body = body;
#endif
    #endregion
}
