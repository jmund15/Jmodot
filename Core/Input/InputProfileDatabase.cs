namespace Jmodot.Core.Input;

using System;
using Godot;
using System.Collections.Generic;
using System.Linq;
using Implementation.Shared;
using Jmodot.Core.Shared.Attributes;
using GCol = Godot.Collections;

/// <summary>
/// An autoload singleton that serves as a central, read-only database for all
/// pre-generated and manually created InputMappingProfiles. At runtime, other managers
/// query this database to get the correct control scheme for a joining player.
/// </summary>
[GlobalClass]
public partial class InputProfileDatabase : Node
{
    /// <summary>
    /// Autoload singleton accessor. Set in <see cref="_EnterTree"/>, cleared in
    /// <see cref="_ExitTree"/>. Consumers should prefer this over
    /// <c>GetNodeOrNull&lt;InputProfileDatabase&gt;("/root/...")</c> magic-path lookups.
    /// May be null during early bootstrap (before any autoload enters the tree) or
    /// after teardown — always null-check before use.
    /// </summary>
    public static InputProfileDatabase? Instance { get; private set; }

    public override void _EnterTree()
    {
        base._EnterTree();
        if (Instance != null)
        {
            JmoLogger.Warning(this, "Duplicate InputProfileDatabase autoload detected; freeing the duplicate.");
            QueueFree();
            return;
        }
        Instance = this;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (Instance == this) { Instance = null; }
    }

    [ExportGroup("Keyboard Profiles")]
    [Export, RequiredExport] private InputMappingProfile _keyboardWasdProfile = null!;
    [Export, RequiredExport] private InputMappingProfile _keyboardArrowsProfile = null!;

    public override void _Ready()
    {
        base._Ready();
        if (Engine.IsEditorHint()) { return; }
        this.ValidateRequiredExports();
    }

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
