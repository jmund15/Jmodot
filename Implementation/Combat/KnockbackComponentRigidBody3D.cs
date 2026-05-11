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
public partial class KnockbackComponentRigidBody3D : Node3D, IComponent
{
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

	[ExportGroup("Behavior")]
	/// <summary>
	/// If true, the Y component of the impulse is zeroed before <see cref="RigidBody3D.ApplyCentralImpulse"/> —
	/// keeps the entity grounded under horizontal pushes.
	/// </summary>
	[Export] public bool FlattenKnockback { get; private set; } = true;

	[ExportGroup("Stats")]
	/// <summary>
	/// Resistance to knockback forces. Resolved via the polymorphic
	/// <see cref="BaseFloatValueDefinition"/> family (constant or stat-driven). Null → 0.
	/// Resistance formula: <c>resistanceFactor = 1 / (1 + stability)</c>.
	/// </summary>
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

	private void OnCombatResult(CombatResult result)
	{
		if (result is IForceCarrier carrier && carrier.Force > 0f)
		{
			ApplyKnockback(carrier.Direction, carrier.Force, result.Source);
		}
	}

	/// <summary>
	/// Applies a knockback impulse (RigidBody regime: <see cref="RigidBody3D.ApplyCentralImpulse"/>
	/// receives N·s and divides by mass internally — no manual mass-division here).
	/// </summary>
	public void ApplyKnockback(Vector3 direction, float incomingForce, Node? attributedSource = null)
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
		var stabilityScaled = StabilityScaling.ScaleForce(direction * incomingForce, stability);
		if (FlattenKnockback)
		{
			stabilityScaled = new Vector3(stabilityScaled.X, 0f, stabilityScaled.Z);
		}

		// RigidBody3D.ApplyCentralImpulse expects N·s and divides by mass internally — do NOT mass-divide here.
		var impulseInNewtonSeconds = stabilityScaled;
		_rigidBody.ApplyCentralImpulse(impulseInNewtonSeconds);

		// Signal payload reports velocity-magnitude (m/s) for unit-consistency with the CharacterBody regime.
		var resultingVelocityDelta = impulseInNewtonSeconds.Length() / Mathf.Max(_rigidBody.Mass, 0.001f);
		EmitSignal(SignalName.KnockbackApplied, direction, resultingVelocityDelta, attributedSource);

		// Audit-log the post-resistance velocity-delta so HSM transition conditions
		// (KnockbackCondition) gate launch/stagger states off the same magnitude the
		// CharacterBody regime sees. RigidBodies typically lack an HSM, so this is usually
		// a no-op — but composite RigidBody-driven actors (e.g., a destructible turret with
		// scripted reaction states) get parity for free.
		_combatLog?.Log(new KnockbackResult
		{
			Source = attributedSource,
			Target = this,
			Direction = direction,
			Force = resultingVelocityDelta,
			Tags = System.Array.Empty<Jmodot.Core.Combat.CombatTag>()
		});

		JmoLogger.Info(this, $"Knockback applied: dir={direction}, |Δv|={resultingVelocityDelta:F2}");
	}

	public override void _ExitTree()
	{
		if (_combatant != null)
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

	#region Test Helpers
#if TOOLS
	internal void SetStability(BaseFloatValueDefinition? value) => Stability = value;
	internal void SetFlattenKnockback(bool value) => FlattenKnockback = value;
#endif
	#endregion
}
