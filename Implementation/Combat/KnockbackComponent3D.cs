using Godot;
using System;
using GCol = Godot.Collections;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;


/// <summary>
/// TODO:
/// Knockback component should listen for combat results from the CombatantComponent,
/// then apply knockback from results as needed.
/// This comp is for CharacterBodies, and a separate one should be made for rigid bodies!
/// This component should also SIGNAL up when knockback is applied!
///
/// REQUIRED BLACKBOARD DEPENDENCIES:
/// - BBDataSig.MovementProcessor (IExampleInterface) - [description of why needed]
///
/// OPTIONAL BLACKBOARD DEPENDENCIES:
/// - BBDataSig.OptionalDependency (IOptionalInterface) - [description]
/// </summary>
[GlobalClass]
public partial class KnockbackComponent3D : Node3D, IComponent
{
	#region DEPENDENCIES
	//private ComponentInitHelper _initHelper = null!;

	// Declare component-specific dependencies retrieved from blackboard
	// Example:
	// private IIntentSource _intentSource = null!;
	// private IStatController _stats = null!;

	#endregion

	#region COMPONENT_VARIABLES

	// Component-specific fields and properties

	// [ExportGroup("Blackboard Registration")] // can remove this if determined that it's not needed
	// [Export]
	// public bool AutoRegister { get; set; } = true; // A checkbox to enable/disable
	// [Export]
	// public StringName BlackboardKey { get; set; } // The key to use for registration

	#endregion

	#region COMPONENT_UPDATES

	public override void _Ready()
	{
		base._Ready();
		// The component disables itself until it's initialized.
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
	}

	#endregion

	#region COMPONENT_LOGIC

	// Component-specific public methods and logic

	#endregion

	#region COMPONENT_HELPER

	// Private helper methods

	#endregion

	#region SIGNAL_LISTENERS

	// Signal handler methods

	#endregion

	#region INTERFACE_IMPLEMENTATION
	public bool IsInitialized { get; private set; }
	public event Action Initialized;

	/// <summary>
	/// Retrieve dependencies from the Blackboard here.
	/// </summary>
	public bool Initialize(IBlackboard bb)
	{
		// Example:
		// if (!bb.TryGet(BBDataSig.IntentSource, out _intentSource))
		// {
		//     JmoLogger.Error(this, "Required dependency BBDataSig.IntentSource not found");
		//     return false;
		// }

		IsInitialized = true;
		Initialized?.Invoke();
		OnPostInitialize();
		return true;
	}
	/// <summary>
	///  Perform setup that relies on other components here (e.g., event subscriptions).
	/// </summary>
	public void OnPostInitialize()
	{
		ProcessMode = ProcessModeEnum.Inherit;
		// Example: _someOtherComponent.OnSomething += HandleSomething;
	}

	public Node GetUnderlyingNode() => this;
	#endregion

	#region CONFIGURATION_WARNINGS

	public override string[] _GetConfigurationWarnings()
	{
		var warnings = new List<string>();

		// Add component-specific warnings
		// Example:
		// if (SomeExportedField == null)
		//     warnings.Add("'SomeExportedField' must be set.");

		return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
	}

	#endregion
}

