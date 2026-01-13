namespace Jmodot.Implementation.Visual.Animation.Model;

using Core.Visual.Model;
using Godot;

/// <summary>
/// A concrete implementation of IMeshComponent using Godot's built-in MeshInstance3D node.
/// </summary>
[GlobalClass]
public partial class MeshInstance3DComponent : MeshInstance3D, IMeshComponent
{
    public void SetMaterialOverride(Material material, int surface = -1)
    {
        if (surface < 0) { base.MaterialOverride = material; }
        else { SetSurfaceOverrideMaterial(surface, material); }
    }

    public void ClearMaterialOverride(int surface = -1)
    {
        if (surface < 0) { base.MaterialOverride = null; }
        else { SetSurfaceOverrideMaterial(surface, null); }
    }

    public Node GetUnderlyingNode() => this;
}
