namespace Jmodot.Core.Input;

[GlobalClass, Tool]
public partial class VectorActionBinding : Resource
{
    [Export] public InputAction Action { get; set; } = null!;

    [ExportGroup("Godot InputMap Actions")]
    [Export] public string Left { get; set; } = null!;
    [Export] public string Right { get; set; } = null!;
    [Export] public string Up { get; set; } = null!;
    [Export] public string Down { get; set; } = null!;
}
