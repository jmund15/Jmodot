namespace Jmodot.Core.Input;

[GlobalClass]
public partial class VectorActionBinding : Resource
{
    [Export] public InputAction Action { get; private set; } = null!;

    [ExportGroup("Godot InputMap Actions")]
    [Export] public string Left { get; private set; } = null!;
    [Export] public string Right { get; private set; } = null!;
    [Export] public string Up { get; private set; } = null!;
    [Export] public string Down { get; private set; } = null!;
}
