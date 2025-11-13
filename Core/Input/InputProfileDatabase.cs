namespace Jmodot.Core.Input;

using System;
using Godot;
using System.Collections.Generic;
using System.Linq;
using Implementation.Shared;
using GCol = Godot.Collections;

/// <summary>
/// An autoload singleton that serves as a central, read-only database for all
/// pre-generated and manually created InputMappingProfiles. At runtime, other managers
/// query this database to get the correct control scheme for a joining player.
/// </summary>
[GlobalClass]
public partial class InputProfileDatabase : Node
{
    [ExportGroup("Keyboard Profiles")]
    [Export] private InputMappingProfile _keyboardWasdProfile;
    [Export] private InputMappingProfile _keyboardArrowsProfile;

    [ExportGroup("Generated Gamepad Profiles")]
    // Drag your generated profiles here in the Inspector, in order:
    // Element 0: gamepad_profile_p0.tres
    // Element 1: gamepad_profile_p1.tres
    // ...and so on.
    [Export] private GCol.Array<InputMappingProfile> _gamepadProfiles = new();

    /// <summary>
    /// Retrieves the pre-generated input profile for a specific gamepad device ID.
    /// </summary>
    /// <param name="deviceId">The device ID of the controller (e.g., 0, 1, 2).</param>
    /// <returns>The corresponding InputMappingProfile, or null if the ID is out of bounds.</returns>
    public InputMappingProfile GetProfileForDevice(int deviceId)
    {
        if (deviceId >= 0 && deviceId < _gamepadProfiles.Count)
        {
            return _gamepadProfiles[deviceId];
        }

        // This can happen if a controller with a high device ID (e.g., 5) connects
        // but you only generated profiles for 0-3.
        JmoLogger.Error(this, $"Request for gamepad profile for device {deviceId} was out of bounds.");
        return null;
    }

    /// <summary>
    /// Retrieves the manually created input profile for a specific keyboard scheme.
    /// </summary>
    /// <param name="type">The type of keyboard layout (WASD or Arrows).</param>
    /// <returns>The corresponding InputMappingProfile.</returns>
    public InputMappingProfile GetProfileForKeyboard(InputDeviceType type)
    {
        return type switch
        {
            InputDeviceType.KeyboardWASD => _keyboardWasdProfile,
            InputDeviceType.KeyboardArrows => _keyboardArrowsProfile,
            _ => throw new NotImplementedException($"Haven't implemented input device type for type '{type}' yet!") // null
        };
    }
}
