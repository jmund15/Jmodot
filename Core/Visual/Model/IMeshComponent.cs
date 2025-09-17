namespace Jmodot.Core.Visual.Model;

using Godot;
using Shared;

/// <summary>
/// Defines a contract for components that represent a 3D mesh.
/// This is the 3D equivalent of ISpriteComponent, allowing for manipulation
/// of materials, visibility, and querying bounding boxes.
/// </summary>
public interface IMeshComponent : IGodotNodeInterface
{
    void SetMaterialOverride(Material material, int surface = -1);
    void ClearMaterialOverride(int surface = -1);
    Aabb GetAabb();
    void Hide();
    void Show();
}
