namespace Jmodot.Implementation.Environment;

using Godot;
using Core.Environment;
using Core.Movement;

/// <summary>
/// A force provider that applies drag force to match target velocity to source velocity.
/// Used for currents, conveyors, and other effects that carry entities along.
/// Force = (sourceVelocity - targetVelocity) * dragRatio
/// </summary>
[GlobalClass]
public partial class VelocityDragForceArea : Area3D, IForceProvider3D
{
    private IVelocityProvider3D? _velocitySource;
    private float _dragRatio = 1.0f;

    /// <summary>
    /// Initialize the velocity drag force area.
    /// </summary>
    /// <param name="velocitySource">The source providing velocity to match.</param>
    /// <param name="dragRatio">Multiplier on velocity difference (1.0 = full match).</param>
    public void Initialize(IVelocityProvider3D velocitySource, float dragRatio)
    {
        _velocitySource = velocitySource;
        _dragRatio = dragRatio;
    }

    /// <summary>
    /// Returns the drag force to apply to a target.
    /// Pulls target velocity toward source velocity.
    /// </summary>
    public Vector3 GetForceFor(Node3D target)
    {
        if (_velocitySource == null)
        {
            return Vector3.Zero;
        }

        var sourceVel = _velocitySource.LinearVelocity;
        var targetVel = GetTargetVelocity(target);

        return (sourceVel - targetVel) * _dragRatio;
    }

    private Vector3 GetTargetVelocity(Node3D? target)
    {
        if (target is IVelocityProvider3D vp)
        {
            return vp.LinearVelocity;
        }

        if (target is CharacterBody3D cb)
        {
            return cb.Velocity;
        }

        return Vector3.Zero;
    }
}
