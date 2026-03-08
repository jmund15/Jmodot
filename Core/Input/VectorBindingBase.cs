namespace Jmodot.Core.Input;

using Shared.Attributes;

[GlobalClass, Tool]
public abstract partial class VectorBindingBase : Resource
{
    [Export, RequiredExport] public InputAction Action { get; set; } = null!;
    public abstract Vector2 GetVectorInput(Node3D entity);
}
