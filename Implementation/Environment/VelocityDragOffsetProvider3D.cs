namespace Jmodot.Implementation.Environment;

using Godot;
using Jmodot.Core.Environment;
using Jmodot.Core.Movement;

/// <summary>
/// Provides a velocity offset that drags targets toward source velocity.
/// Friction-independent: all entities are carried at the same rate regardless of their friction.
/// DragRatio is intuitive: 1.0 = match source speed exactly, 0.5 = half source speed.
/// </summary>
[GlobalClass]
public partial class VelocityDragOffsetProvider3D : Area3D, IVelocityOffsetProvider3D
{
    private IVelocityProvider3D? _velocitySource;
    private float _dragRatio = 1.0f;

    public void Initialize(IVelocityProvider3D velocitySource, float dragRatio)
    {
        _velocitySource = velocitySource;
        _dragRatio = Mathf.Clamp(dragRatio, 0f, 1f);
    }

    /// <summary>
    /// Update drag ratio at runtime (e.g., tier-based scaling).
    /// </summary>
    public void SetDragRatio(float dragRatio)
    {
        _dragRatio = Mathf.Clamp(dragRatio, 0f, 1f);
    }

    public Vector3 GetVelocityOffsetFor(Node3D target)
    {
        if (_velocitySource == null)
        {
            return Vector3.Zero;
        }

        // Return offset = source velocity * drag ratio
        // This gets ADDED to whatever the player is doing
        return _velocitySource.LinearVelocity * _dragRatio;
    }
}
