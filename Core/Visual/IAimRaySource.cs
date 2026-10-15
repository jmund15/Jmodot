namespace Jmodot.Core.Visual;

using Godot;

/// <summary>
/// A camera-local aim ray: where the view starts, which way it points, and whether it is
/// orthographic (an orthographic ray starts on the near plane, not at the camera).
/// </summary>
public readonly record struct AimRay(Vector3 Origin, Vector3 Direction, bool Orthographic);

/// <summary>
/// A 3D camera that answers aim rays from its authored view, not from the pose it is showing.
/// A camera that shakes or kicks implements this so mouse aim keeps the un-impulsed ray.
/// </summary>
public interface IAimRaySource
{
    /// <summary>The aim ray through a viewport point, as if no impulse were applied.</summary>
    AimRay ProjectRay(Vector2 screenPoint);
}
