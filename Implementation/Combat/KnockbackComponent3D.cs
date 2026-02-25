using Godot;
using System;
using GCol = Godot.Collections;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Actors;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Implementation.Actors;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

namespace Jmodot.Implementation.Combat;

/// <summary>
/// Handles knockback application for CharacterBody3D entities.
/// Subscribes to CombatantComponent.CombatResultEvent and applies impulses
/// immediately via MovementProcessor3D when DamageResult contains force.
///
/// REQUIRED BLACKBOARD DEPENDENCIES:
/// - BBDataSig.MovementProcessor (IMovementProcessor3D) - Applies the impulse
/// - BBDataSig.CombatantComponent (CombatantComponent) - Source of combat events
/// </summary>
[GlobalClass]
public partial class KnockbackComponent3D : Node3D, IComponent, IBlackboardProvider
{
	#region IBlackboardProvider Implementation
	public (StringName Key, object Value)? Provision => (BBDataSig.KnockbackComponent, this);
	#endregion

	#region SIGNALS

	/// <summary>
	/// Emitted when knockback is applied. Useful for VFX, audio, or other reactive systems.
	/// </summary>
	[Signal] public delegate void KnockbackAppliedEventHandler(Vector3 direction, float force);

	#endregion

	#region DEPENDENCIES

	private IMovementProcessor3D _movementProcessor = null!;
	private CombatantComponent _combatant = null!;

	#endregion

	#region COMPONENT_VARIABLES

	/// <summary>
	/// If true, the Y component of knockback is zeroed out (typical for grounded characters).
	/// </summary>
	[Export] public bool FlattenKnockback { get; set; } = true;

	/// <summary>
	/// Resistance to knockback forces. Uses diminishing returns formula:
	/// resistanceFactor = 1 / (1 + stability).
	/// 0 = no resistance (full knockback), 1 = half force, 3 = quarter force.
	/// Same formula used by PushableComponent3D for consistent resistance.
	/// </summary>
	[Export(PropertyHint.Range, "0,20,0.1")] public float Stability { get; set; }

	#endregion

	#region COMPONENT_UPDATES

	public override void _Ready()
	{
		base._Ready();
		// The component disables itself until it's initialized.
		ProcessMode = ProcessModeEnum.Disabled;
	}

	#endregion

	#region COMPONENT_LOGIC

	/// <summary>
	/// Called when a combat result is received from the CombatantComponent.
	/// Extracts knockback data from DamageResult and applies impulse.
	/// </summary>
	private void OnCombatResult(CombatResult result)
	{
		if (result is DamageResult damageResult && damageResult.Force > 0)
		{
			ApplyKnockback(damageResult.Direction, damageResult.Force);
		}
	}

	/// <summary>
	/// Applies a knockback impulse to the MovementProcessor.
	/// </summary>
	/// <param name="direction">The normalized direction of the knockback.</param>
	/// <param name="force">The magnitude of the knockback force.</param>
	public void ApplyKnockback(Vector3 direction, float force)
	{
		var scaledForce = force * StabilityScaling.CalculateResistanceFactor(Stability);
		var impulse = direction * scaledForce;

		if (FlattenKnockback)
		{
			impulse = new Vector3(impulse.X, 0f, impulse.Z);
		}

		_movementProcessor.ApplyImpulse(impulse);
		EmitSignal(SignalName.KnockbackApplied, direction, scaledForce);
	}

	public override void _ExitTree()
	{
		if (_combatant != null)
		{
			_combatant.CombatResultEvent -= OnCombatResult;
		}
	}

	#endregion

	#region INTERFACE_IMPLEMENTATION

	public bool IsInitialized { get; private set; }
	public event Action Initialized = delegate { };

	/// <summary>
	/// Retrieve dependencies from the Blackboard here.
	/// </summary>
	public bool Initialize(IBlackboard bb)
	{
		if (!bb.TryGet(BBDataSig.MovementProcessor, out _movementProcessor!))
		{
			JmoLogger.Error(this, "Required dependency BBDataSig.MovementProcessor not found");
			return false;
		}

		if (!bb.TryGet(BBDataSig.CombatantComponent, out _combatant!))
		{
			JmoLogger.Error(this, "Required dependency BBDataSig.CombatantComponent not found");
			return false;
		}

		IsInitialized = true;
		Initialized();
		OnPostInitialize();
		return true;
	}

	/// <summary>
	/// Perform setup that relies on other components here (e.g., event subscriptions).
	/// </summary>
	public void OnPostInitialize()
	{
		ProcessMode = ProcessModeEnum.Inherit;
		_combatant.CombatResultEvent += OnCombatResult;
	}

	public Node GetUnderlyingNode() => this;

	#endregion

	#region CONFIGURATION_WARNINGS

	public override string[] _GetConfigurationWarnings()
	{
		var warnings = new List<string>();

		// Configuration warnings will be shown in editor
		// Dependencies are retrieved from Blackboard at runtime

		return warnings.Concat(base._GetConfigurationWarnings() ?? []).ToArray();
	}

	#endregion
}
