namespace Jmodot.Core.Environment;

using Godot;

/// <summary>
/// Provides a velocity offset that is added fresh each frame, NOT stored in the controller.
/// Use for effects that should be friction-independent (conveyors, currents, wave drag).
/// Unlike IForceProvider3D, offsets don't accumulate or get dampened by friction.
/// </summary>
public interface IVelocityOffsetProvider3D
{
    /// <summary>
    /// Calculates the velocity offset to apply to a target this frame.
    /// </summary>
    /// <param name="target">The actor being affected.</param>
    /// <returns>Velocity offset to add for this frame's Move() call only.</returns>
    Vector3 GetVelocityOffsetFor(Node3D target);
}
