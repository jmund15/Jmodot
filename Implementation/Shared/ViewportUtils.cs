namespace Jmodot.Implementation.Shared;

using Godot;

public static class ViewportUtils
{
    private static Camera3D? _cachedCamera;
    private static Window? _cachedRoot;

    /// <summary>
    /// Pre-populate the viewport cache from a loaded node context.
    /// Call from your autoload's _Ready() to avoid per-frame lazy discovery.
    /// </summary>
    public static void RegisterViewport(Window root, Camera3D camera)
    {
        _cachedRoot = root;
        _cachedCamera = camera;
    }

    public static Vector3 GetMouseWorldPosition3D()
    {
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree == null)
        {
            return Vector3.Zero;
        }

        if (_cachedRoot == null || !GodotObject.IsInstanceValid(_cachedRoot))
        {
            _cachedRoot = sceneTree.GetRoot();
        }

        if (_cachedCamera == null || !GodotObject.IsInstanceValid(_cachedCamera))
        {
            _cachedCamera = _cachedRoot.GetViewport().GetCamera3D();
            if (_cachedCamera == null)
            {
                // Expected during _Ready() before main scene camera exists — not an error.
                return Vector3.Zero;
            }
        }

        var mousePosition = _cachedRoot.GetMousePosition();

        var rayOrigin = _cachedCamera.ProjectRayOrigin(mousePosition);
        var rayDirection = _cachedCamera.ProjectRayNormal(mousePosition);

        // construct floor plane (only care about x/z mouse direction, no height)
        var floorPlane = new Plane(Vector3.Up, 0);

        // the 3d point (where y = 0 which is 'floorPlane's origin)
        var groundIntersection = floorPlane.IntersectsRay(rayOrigin, rayDirection);

        if (groundIntersection == null)
        {
            JmoLogger.Error(_cachedRoot,
                $"Couldn't find mouse intersection for origin '{rayOrigin}' and direction '{rayDirection}'");
            return Vector3.Zero;
        }
        return groundIntersection.Value;
    }
}
