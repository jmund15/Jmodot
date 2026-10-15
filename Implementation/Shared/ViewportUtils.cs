namespace Jmodot.Implementation.Shared;

using Godot;
using Jmodot.Core.Visual;

public static class ViewportUtils
{
    /// <summary>
    /// Projects the mouse onto the y = 0 floor plane through the root viewport's current camera.
    /// Returns false when there is no answer: no scene tree, no current camera (e.g. during a
    /// scene swap or before the first scene's camera exists), or a ray that misses the floor.
    /// The camera is resolved on every call; a cached camera goes stale when its scene is
    /// removed from the tree before it is freed.
    /// </summary>
    public static bool TryGetMouseWorldPosition3D(out Vector3 position)
    {
        position = Vector3.Zero;
        if (Engine.GetMainLoop() is not SceneTree sceneTree)
        {
            return false;
        }

        var root = sceneTree.Root;
        var camera = root.GetCamera3D();
        if (camera == null)
        {
            return false;
        }

        var mousePosition = root.GetMousePosition();
        var ray = camera is IAimRaySource source
            ? source.ProjectRay(mousePosition)
            : new AimRay(camera.ProjectRayOrigin(mousePosition), camera.ProjectRayNormal(mousePosition),
                camera.Projection == Camera3D.ProjectionType.Orthogonal);

        if (!TryIntersectFloor(ray.Origin, ray.Direction, ray.Orthographic, out position))
        {
            JmoLogger.Error(root,
                $"Couldn't find mouse intersection for origin '{ray.Origin}' and direction '{ray.Direction}'");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Intersects a camera ray with the y = 0 floor plane. An orthographic ray starts on the near
    /// plane, which lies below the floor when the cursor is past a low camera's lower view edge, so
    /// its whole line counts and a hit behind <paramref name="origin"/> is valid. A perspective ray
    /// counts only forward of its origin. Returns false when the ray is parallel to the floor.
    /// </summary>
    public static bool TryIntersectFloor(Vector3 origin, Vector3 direction, bool orthographic, out Vector3 point)
    {
        point = Vector3.Zero;
        if (Mathf.IsZeroApprox(direction.Y))
        {
            return false;
        }

        var distance = -origin.Y / direction.Y;
        if (!orthographic && distance < 0f)
        {
            return false;
        }

        point = origin + direction * distance;
        return true;
    }
}
