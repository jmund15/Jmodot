namespace Jmodot.Core.Input;

using Jmodot.Core.Shared.Attributes;

[GlobalClass, Tool]
public partial class VectorActionBinding : Resource
{
    [Export, RequiredExport] public InputAction Action { get; set; } = null!;

    [ExportGroup("Godot InputMap Actions")]
    [Export, RequiredExport] public string Left { get; set; } = null!;
    [Export, RequiredExport] public string Right { get; set; } = null!;
    [Export, RequiredExport] public string Up { get; set; } = null!;
    [Export, RequiredExport] public string Down { get; set; } = null!;
}
