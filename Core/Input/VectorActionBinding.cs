namespace Jmodot.Core.Input;

using Jmodot.Core.Shared.Attributes;
using Input = Godot.Input;

[GlobalClass, Tool]
public partial class VectorActionBinding : VectorBindingBase
{
    [ExportGroup("Godot InputMap Actions")]
    [Export, RequiredExport] public string Left { get; set; } = null!;
    [Export, RequiredExport] public string Right { get; set; } = null!;
    [Export, RequiredExport] public string Up { get; set; } = null!;
    [Export, RequiredExport] public string Down { get; set; } = null!;
    public override Vector2 GetVectorInput(Node3D entity)
    {
        return Input.GetVector(Left, Right, Up, Down);
    }
}
