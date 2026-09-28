namespace Jmodot.Implementation.Shared;

using Godot;

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
        var rayOrigin = camera.ProjectRayOrigin(mousePosition);
        var rayDirection = camera.ProjectRayNormal(mousePosition);

        var groundIntersection = new Plane(Vector3.Up, 0).IntersectsRay(rayOrigin, rayDirection);
        if (groundIntersection == null)
        {
            JmoLogger.Error(root,
                $"Couldn't find mouse intersection for origin '{rayOrigin}' and direction '{rayDirection}'");
            return false;
        }

        position = groundIntersection.Value;
        return true;
    }
}
