namespace Jmodot.Core.Input;
// InputProfileDatabase.cs (Autoload Singleton)

using Godot;
using System.Collections.Generic;
using System.Linq;

// An enum to clearly define our supported input types, decoupling us from Godot device IDs.
public enum InputDeviceType
{
    KeyboardWASD,
    KeyboardArrows,
    Gamepad
}

[GlobalClass]
public partial class InputProfileDatabase : Node
{
    [Export] private InputMappingProfile _keyboardWasdProfile;
    [Export] private InputMappingProfile _keyboardArrowsProfile;

    // Drag your 4 or 8 generated profiles here in the Inspector, in order (p0, p1, p2...)
    [Export] private Godot.Collections.Array<InputMappingProfile> _gamepadProfiles = new();

    public InputMappingProfile GetProfileForDevice(int deviceId)
    {
        if (deviceId >= 0 && deviceId < _gamepadProfiles.Count)
        {
            return _gamepadProfiles[deviceId];
        }
        return null; // No profile for this device
    }

    public InputMappingProfile GetProfileForKeyboard(InputDeviceType type)
    {
        return type == InputDeviceType.KeyboardWASD ? _keyboardWasdProfile : _keyboardArrowsProfile;
    }
}
