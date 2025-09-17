namespace Jmodot.Core.Input;

/// <summary>
///     A data-driven Resource that represents a unique, abstract input action.
///     This allows systems to query for intent (e.g., "Jump", "Move") in a standardized,
///     type-safe way, without being coupled to physical buttons or Godot's InputMap strings.
///     Its primary purpose is to serve as a reliable key in dictionaries and for tooling.
/// </summary>
[GlobalClass]
public sealed partial class InputAction : Resource // Sealed to prevent inheritance; it's a pure data key.
{
    /// <summary>
    ///     The user-friendly name of the action for debugging, and for use in tools
    ///     like an Input Re-binding screen UI.
    /// </summary>
    [Export]
    public string ActionName { get; private set; } = "Unnamed Action";
}
