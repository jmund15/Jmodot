namespace Jmodot.Implementation.Shared;

using Godot;
public class ViewportUtils
{
    public static Vector3 GetMouseWorldPosition3D()
    {
        // TODO: have a global caching of the current root / camera 3d for easy lookup
        var sceneTreeRoot = (Engine.GetMainLoop() as SceneTree)!.GetRoot();
        var camera3d = sceneTreeRoot.GetViewport().GetCamera3D();

        var mousePosition = sceneTreeRoot.GetMousePosition();

        var rayOrigin = camera3d.ProjectRayOrigin(mousePosition);
        var rayDirection = camera3d.ProjectRayNormal(mousePosition);

        // construct floor plane (only care about x/z mouse direction, no height)
        var floorPlane = new Plane(Vector3.Up, 0);

        // the 3d point (where y = 0 which is 'floorPlane's origin)
        var groundIntersection = floorPlane.IntersectsRay(rayOrigin, rayDirection);

        if (groundIntersection == null)
        {
            JmoLogger.Error(sceneTreeRoot,
                $"Couldn't find mouse intersection for origin '{rayOrigin}' and direction '{rayDirection}'");
            return Vector3.Zero;
        }
        return groundIntersection.Value;
    }
}
