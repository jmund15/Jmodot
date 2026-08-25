using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Actors;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

namespace Jmodot.Implementation.Combat;

/// <summary>
/// Handles knockback application for <see cref="RigidBody3D"/> entities. Subscribes to
/// <see cref="CombatantComponent.CombatResultEvent"/> and applies impulses via
/// <see cref="RigidBody3D.ApplyCentralImpulse"/> when a result implements
/// <see cref="IForceCarrier"/>.
///
/// REGIME DIFFERENCE vs <see cref="KnockbackComponent3D"/>:
/// - RigidBody3D has its own <see cref="RigidBody3D.Mass"/> property; no <c>Mass</c> export here.
/// - <see cref="RigidBody3D.ApplyCentralImpulse"/> divides by mass internally — DO NOT pre-divide.
///
/// REQUIRED BLACKBOARD DEPENDENCIES:
/// - BBDataSig.CombatantComponent (CombatantComponent) — source of combat events.
///
/// OPTIONAL BLACKBOARD DEPENDENCIES:
/// - BBDataSig.Stats (IStatProvider) — feeds <see cref="AttributeFloatDefinition"/> resolution
///   for <see cref="Stability"/>. ConstantFloatDefinition users sidestep this.
/// </summary>
[GlobalClass]
public partial class KnockbackComponentRigidBody3D : Node3D, IComponent, IBlackboardProvider, IKnockbackReceiver3D
{
	#region IBlackboardProvider Implementation
	// Publishes the same key as the CharacterBody regime. Without it a rigid-body actor never
	// reaches the blackboard, and every consumer resolving BBDataSig.KnockbackComponent — the
	// attachment rider's shed fling above all — silently skips it with no error and no log.
	public (StringName Key, object Value)? Provision => (BBDataSig.KnockbackComponent, this);
	#endregion

	#region SIGNALS

	/// <summary>
	/// Emitted when knockback is applied. Magnitude is in m/s velocity-delta units (N·s impulse
	/// divided by RigidBody mass), consistent with the CharacterBody regime's signal payload.
	/// </summary>
	[Signal] public delegate void KnockbackAppliedEventHandler(Vector3 direction, float magnitude, Node? attributedSource);

	#endregion

	#region DEPENDENCIES

	private RigidBody3D _rigidBody = null!;
	private CombatantComponent _combatant = null!;
	private IStatProvider? _statProvider; // Soft dep — null is acceptable for ConstantFloatDefinition users.
	private CombatLog? _combatLog;        // Soft dep — null is acceptable for HSM-less receivers.

	#endregion

	#region COMPONENT_VARIABLES

	/// <summary>
	/// Reference to the RigidBody3D that receives impulses.
	/// </summary>
	[Export, RequiredExport] public RigidBody3D TargetRigidBody { get; set; } = null!;

	/// <summary>
	/// When true this receiver keeps the Y component of the impulse passed to
	/// <see cref="RigidBody3D.ApplyCentralImpulse"/>. Default false zeroes it — the safety net
	/// against sloppy producers, keeping the body grounded under horizontal pushes.
	///
	/// Shares its name and polarity with
	/// <see cref="Jmodot.Core.Combat.Reactions.KnockbackResult.PreserveVertical"/> and the
	/// <c>preserveVertical</c> parameter of
	/// <see cref="ApplyKnockback(Vector3, float, Node, bool)"/>: all three are ORed, so either the
	/// receiver or the producer may assert the vertical and neither can veto it.
	/// </summary>
	[ExportGroup("Behavior")]
	[Export] public bool PreserveVertical { get; private set; }

	/// <summary>
	/// Resistance to knockback forces. Resolved via the polymorphic
	/// <see cref="BaseFloatValueDefinition"/> family (constant or stat-driven). Null → 0.
	/// Resistance formula: <c>resistanceFactor = 1 / (1 + stability)</c>.
	/// </summary>
	[ExportGroup("Stats")]
	[Export] public BaseFloatValueDefinition? Stability { get; private set; }

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

	/// <remarks>
	/// <c>PreserveVertical</c> is read off <see cref="KnockbackResult"/> when present and forwarded
	/// to <see cref="ApplyKnockback(Vector3, float, Node, bool)"/>, mirroring
	/// <see cref="KnockbackComponent3D"/> — identical authored data must behave identically on
	/// either body regime. Other <see cref="IForceCarrier"/> types default to false and continue
	/// to flatten unless <see cref="PreserveVertical"/> is set on the receiver.
	/// </remarks>
	private void OnCombatResult(CombatResult result)
	{
		if (result is not IForceCarrier carrier || carrier.Force <= 0f) { return; }

		var preserveVertical = (result as KnockbackResult)?.PreserveVertical ?? false;
		ApplyKnockback(carrier.Direction, carrier.Force, result.Source, preserveVertical);
	}

	/// <summary>
	/// Applies a knockback impulse (RigidBody regime: <see cref="RigidBody3D.ApplyCentralImpulse"/>
	/// receives N·s and divides by mass internally — no manual mass-division here).
	/// </summary>
	/// <param name="direction">Normalized direction of the knockback.</param>
	/// <param name="incomingForce">Impulse magnitude in N·s.</param>
	/// <param name="attributedSource">Originating cause for HSM transition / VFX / audio chain attribution.</param>
	/// <param name="preserveVertical">
	/// When true, the safety-net flatten is bypassed — the source has stamped Direction.Y as
	/// intentional (e.g., a rock pillar's rising pop). ORed with the receiver's own
	/// <see cref="PreserveVertical"/>: false here still preserves Y if the receiver asks for it.
	/// </param>
	public void ApplyKnockback(Vector3 direction, float incomingForce, Node? attributedSource = null, bool preserveVertical = false)
	{
		if (_rigidBody == null)
		{
			JmoLogger.Error(this, "No RigidBody3D assigned!");
			return;
		}

		if (!float.IsFinite(incomingForce) || incomingForce <= 0f)
		{
			JmoLogger.Warning(this, $"Knockback skipped: invalid force={incomingForce:F2}.");
			return;
		}

		var stability = Stability?.ResolveFloatValue(_statProvider) ?? 0f;
		var resolved = KnockbackPolicy.Resolve(direction, incomingForce, stability, PreserveVertical || preserveVertical);

		// RigidBody3D.ApplyCentralImpulse expects N·s and divides by mass internally — do NOT mass-divide here.
		_rigidBody.ApplyCentralImpulse(resolved.Impulse);

		// Signal payload reports velocity-magnitude (m/s) for unit-consistency with the CharacterBody regime.
		var resultingVelocityDelta = resolved.Impulse.Length() / Mathf.Max(_rigidBody.Mass, 0.001f);
		EmitSignal(SignalName.KnockbackApplied, resolved.AppliedDirection, resultingVelocityDelta, attributedSource);
		KnockbackPolicy.LogApplied(_combatLog, this, attributedSource, resolved.AppliedDirection, resultingVelocityDelta);

		JmoLogger.Info(this, $"[Impact] Knockback applied: dir={resolved.AppliedDirection}, |Δv|={resultingVelocityDelta:F2}");
	}

	public override void _ExitTree()
	{
		// Disposal-race guard, mirroring KnockbackComponent3D: CombatantComponent is a Godot
		// Node and sibling free-order during teardown is not guaranteed, so it may already be
		// freed. A freed Node's managed wrapper stays non-null, so the null check alone does not
		// see it and the unsubscribe throws ObjectDisposedException.
		if (_combatant != null && GodotObject.IsInstanceValid(_combatant))
		{
			_combatant.CombatResultEvent -= OnCombatResult;
		}
		base._ExitTree();
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

		// Soft dep — null is acceptable. AttributeFloatDefinition.ResolveFloatValue handles null safely.
		bb.TryGet(BBDataSig.Stats, out _statProvider);

		// Soft dep — null is acceptable. RigidBodies typically lack an HSM/CombatLog.
		bb.TryGet(BBDataSig.CombatLog, out _combatLog);

		IsInitialized = true;
		Initialized();
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

	#region Test Helpers
#if TOOLS
	internal void SetStability(BaseFloatValueDefinition? value) => Stability = value;
	internal void SetPreserveVertical(bool value) => PreserveVertical = value;
#endif
	#endregion
}
