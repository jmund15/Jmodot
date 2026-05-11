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
	/// Same formula used by PhysicsInteractionComponent for consistent resistance.
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
	/// Extracts knockback data from DamageResult OR KnockbackResult (pure-knockback
	/// effects like environment deposits' impact areas) and applies impulse.
	/// </summary>
	/// <remarks>
	/// 2026-05-09 fix: previously only DamageResult was handled, silently dropping
	/// every KnockbackResult from KnockbackEffect-only sources (rock pillar, future
	/// non-damaging environmental impulses). Both result types carry Direction +
	/// Force in equivalent semantics, so they unify here. Source order is
	/// observation-only — no priority intended.
	/// </remarks>
	private void OnCombatResult(CombatResult result)
	{
		if (result is DamageResult damageResult && damageResult.Force > 0)
		{
			// DamageResult doesn't carry PreserveVertical — damage-knockback continues
			// to default-flatten (existing behavior preserved for fireball/etc).
			ApplyKnockback(damageResult.Direction, damageResult.Force);
		}
		else if (result is KnockbackResult knockbackResult && knockbackResult.Force > 0)
		{
			// KnockbackResult.PreserveVertical is set by producers like KnockbackEffect
			// with UpwardAngleDegrees > 0 — the rising rock pillar's signal that its Y
			// component is intentional and must NOT be flattened by the receiver.
			ApplyKnockback(knockbackResult.Direction, knockbackResult.Force, knockbackResult.PreserveVertical);
		}
	}

	/// <summary>
	/// Applies a knockback impulse to the MovementProcessor.
	/// </summary>
	/// <param name="direction">The normalized direction of the knockback.</param>
	/// <param name="force">The magnitude of the knockback force.</param>
	/// <param name="preserveVertical">
	/// When true, the receiver's <see cref="FlattenKnockback"/> safety-net flatten is bypassed
	/// — the source has stamped the Direction.Y as intentional. When false (default),
	/// FlattenKnockback (if true) zeros the Y component as before.
	/// </param>
	public void ApplyKnockback(Vector3 direction, float force, bool preserveVertical = false)
	{
		var scaledForce = force * StabilityScaling.CalculateResistanceFactor(Stability);
		var impulse = direction * scaledForce;

		if (FlattenKnockback && !preserveVertical)
		{
			impulse = new Vector3(impulse.X, 0f, impulse.Z);
		}

		_movementProcessor.ApplyImpulse(impulse);
		EmitSignal(SignalName.KnockbackApplied, direction, scaledForce);
	}

	public override void _ExitTree()
	{
		// Disposal-race guard: CombatantComponent is a Godot Node and may be freed
		// before this _ExitTree fires during scene teardown (sibling free-order is
		// not guaranteed). Unsubscribing from a disposed event source throws
		// ObjectDisposedException. See BB_Apply_Cache_Restore_Pattern (IsInstanceValid).
		if (_combatant != null && GodotObject.IsInstanceValid(_combatant))
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
		if (!bb.TryGet(BBDataSig.MovementProcessor, out _movementProcessor!) || _movementProcessor == null)
		{
			JmoLogger.Error(this, "Required dependency BBDataSig.MovementProcessor not found");
			return false;
		}

		if (!bb.TryGet(BBDataSig.CombatantComponent, out _combatant!) || _combatant == null)
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

#if TOOLS
	#region Test Helpers

	// Logic-Domain tests construct the component without going through the BB wiring
	// pipeline (Initialize). This injects the movement processor directly so the
	// FlattenKnockback × PreserveVertical gate's pure-math behavior can be exercised
	// without a full CombatantComponent + Blackboard fixture. Compiled out of release
	// builds via #if TOOLS.
	internal void SetMovementProcessorForTesting(IMovementProcessor3D processor) =>
		_movementProcessor = processor;

	#endregion
#endif
}
