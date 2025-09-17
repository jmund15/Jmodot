namespace Jmodot.Core.Input;

/// <summary>Defines the link between an abstract action and a Godot InputMap string.</summary>
[GlobalClass]
public partial class ActionBinding : Resource
{
    [Export] public InputAction Action { get; private set; } = null!;

    /// <summary>
    ///     The name of the action as defined in Godot's InputMap.
    /// </summary>
    [Export]
    public string GodotActionName { get; private set; } = "";

    [Export] public InputActionPollType PollType { get; private set; } = InputActionPollType.JustPressed;
}
