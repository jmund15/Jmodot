using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Actors;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

namespace Jmodot.Implementation.Combat;

/// <summary>
/// Placeholder for knockback handling on RigidBody3D entities.
/// TODO: Implement knockback using RigidBody3D.ApplyCentralImpulse() or ApplyImpulse().
///
/// REQUIRED BLACKBOARD DEPENDENCIES:
/// - BBDataSig.CombatantComponent (CombatantComponent) - Source of combat events
/// </summary>
[GlobalClass]
public partial class KnockbackComponentRigidBody3D : Node3D, IComponent
{
	#region SIGNALS

	/// <summary>
	/// Emitted when knockback is applied. Useful for VFX, audio, or other reactive systems.
	/// </summary>
	[Signal] public delegate void KnockbackAppliedEventHandler(Vector3 direction, float force);

	#endregion

	#region DEPENDENCIES

	private RigidBody3D _rigidBody = null!;
	private CombatantComponent _combatant = null!;

	#endregion

	#region COMPONENT_VARIABLES

	/// <summary>
	/// Reference to the RigidBody3D to apply impulses to.
	/// </summary>
	[Export, RequiredExport] public RigidBody3D TargetRigidBody { get; set; } = null!;

	/// <summary>
	/// Resistance to knockback forces. Uses diminishing returns formula:
	/// resistanceFactor = 1 / (1 + stability).
	/// 0 = no resistance, 1 = half force, 3 = quarter force.
	/// </summary>
	[Export(PropertyHint.Range, "0,20,0.1")] public float Stability { get; set; }

	#endregion

	#region COMPONENT_UPDATES

	public override void _Ready()
	{
		base._Ready();
		if (Engine.IsEditorHint()) { return; }
		this.ValidateRequiredExports();
		ProcessMode = ProcessModeEnum.Disabled;
	}

	#endregion

	#region COMPONENT_LOGIC

	private void OnCombatResult(CombatResult result)
	{
		if (result is DamageResult damageResult && damageResult.Force > 0)
		{
			ApplyKnockback(damageResult.Direction, damageResult.Force);
		}
	}

	public void ApplyKnockback(Vector3 direction, float force)
	{
		if (_rigidBody == null)
		{
			JmoLogger.Error(this, "No RigidBody3D assigned!");
			return;
		}

		var scaledForce = force * StabilityScaling.CalculateResistanceFactor(Stability);
		var impulse = direction * scaledForce;

		// TODO: Implement actual impulse application
		// _rigidBody.ApplyCentralImpulse(impulse);

		EmitSignal(SignalName.KnockbackApplied, direction, scaledForce);
		JmoLogger.Info(this, $"TODO: Apply impulse {impulse} to RigidBody3D");
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

	public bool Initialize(IBlackboard bb)
	{
		if (!bb.TryGet(BBDataSig.CombatantComponent, out _combatant!) || _combatant == null)
		{
			JmoLogger.Error(this, "Required dependency BBDataSig.CombatantComponent not found");
			return false;
		}

		_rigidBody = TargetRigidBody;

		IsInitialized = true;
		Initialized();
		OnPostInitialize();
		return true;
	}

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

		if (TargetRigidBody == null)
		{
			warnings.Add("'TargetRigidBody' must be assigned for knockback to work.");
		}

		return warnings.Concat(base._GetConfigurationWarnings() ?? []).ToArray();
	}

	#endregion
}
