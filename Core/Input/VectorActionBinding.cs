namespace Jmodot.Core.Input;

[GlobalClass]
public partial class VectorActionBinding : Resource
{
    [Export] public InputAction Action { get; private set; } = null!;

    [ExportGroup("Godot InputMap Actions")]
    [Export]
    public string Left { get; private set; } = "move_left";

    [Export] public string Right { get; private set; } = "move_right";
    [Export] public string Up { get; private set; } = "move_up";
    [Export] public string Down { get; private set; } = "move_down";
}
