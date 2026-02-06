namespace Jmodot.Core.Input;

using Jmodot.Core.Shared.Attributes;

/// <summary>Defines the link between an abstract action and a Godot InputMap string.</summary>
[GlobalClass, Tool]
public partial class ActionBinding : Resource
{
    [Export, RequiredExport] public InputAction Action { get; set; } = null!;

    /// <summary>
    ///     The name of the action as defined in Godot's InputMap.
    /// </summary>
    [Export]
    public string GodotActionName { get; set; } = "";

    [Export] public InputActionPollType PollType { get; set; } = InputActionPollType.JustPressed;
}
