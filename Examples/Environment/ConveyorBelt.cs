#region

using Jmodot.Core.Environment;

#endregion

namespace Jmodot.Examples.Environment;

/// <summary>
///     A concrete implementation of IForceProvider. This Area3D acts as a conveyor belt,
///     applying a constant directional force to any actor with an ExternalForceReceiver
///     that enters its volume.
/// </summary>
[GlobalClass]
public partial class ConveyorBelt : Area3D, IForceProvider
{
    private Vector3 _globalPushVelocity;

    /// <summary>The direction the belt pushes, relative to its own orientation.</summary>
    [Export] private Vector3 _localPushDirection = Vector3.Forward;

    /// <summary>The speed of the belt's push in units per second.</summary>
    [Export] private float _pushSpeed = 5.0f;

    /// <inheritdoc />
    public Vector3 GetForceFor(Node3D target)
    {
        // For a simple conveyor belt, the force is constant regardless of the target.
        return _globalPushVelocity;
    }

    public override void _Ready()
    {
        // TODO: FIX
        // Cache the global direction on ready to avoid recalculating it every frame.
        //_globalPushVelocity = Basis.Transform(_localPushDirection).Normalized() * _pushSpeed;
    }
}