namespace Jmodot.Core.Input;

public enum InputActionPollType
{
    /// <summary>Checks for a single-frame press event (Input.IsActionJustPressed).</summary>
    JustPressed,

    /// <summary>Checks if the action is currently held down (Input.IsActionPressed).</summary>
    Pressed,

    /// <summary>Checks for a single-frame release event (Input.IsActionJustReleased).</summary>
    JustReleased
}