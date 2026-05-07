using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Actors;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Actors;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

namespace Jmodot.Implementation.Combat;

/// <summary>
/// Handles knockback application for CharacterBody3D entities. Subscribes to
/// <see cref="CombatantComponent.CombatResultEvent"/> and applies impulses immediately via
/// <see cref="IMovementProcessor3D"/> when a result implements <see cref="IForceCarrier"/>.
///
/// REQUIRED BLACKBOARD DEPENDENCIES:
/// - BBDataSig.MovementProcessor (IMovementProcessor3D) — applies the impulse.
/// - BBDataSig.CombatantComponent (CombatantComponent) — source of combat events.
///
/// OPTIONAL BLACKBOARD DEPENDENCIES:
/// - BBDataSig.Stats (IStatProvider) — feeds <see cref="AttributeFloatDefinition"/> resolution
///   for <see cref="Stability"/> / <see cref="Mass"/>. ConstantFloatDefinition users sidestep this.
/// </summary>
[GlobalClass]
public partial class KnockbackComponent3D : Node3D, IComponent, IBlackboardProvider
{
	#region IBlackboardProvider Implementation
	public (StringName Key, object Value)? Provision => (BBDataSig.KnockbackComponent, this);
	#endregion

	#region SIGNALS

	/// <summary>
	/// Emitted when knockback is applied. Payload is in m/s velocity-delta units (consistent
	/// across CharacterBody and RigidBody regimes). <c>attributedSource</c> propagates the
	/// originating cause (e.g., the wizard who cast the spell) so HSM transition conditions,
	/// VFX, and audio can attribute-chain.
	/// </summary>
	[Signal] public delegate void KnockbackAppliedEventHandler(Vector3 direction, float magnitude, Node? attributedSource);

	#endregion

	#region DEPENDENCIES

	private IMovementProcessor3D _movementProcessor = null!;
	private CombatantComponent _combatant = null!;
	private IStatProvider? _statProvider; // Soft dep — null is acceptable for ConstantFloatDefinition users.

	#endregion

	#region COMPONENT_VARIABLES

	[ExportGroup("Behavior")]
	/// <summary>
	/// If true, the Y component of the resulting velocity-delta is zeroed out (typical for
	/// grounded characters that should be pushed horizontally, not lifted).
	/// </summary>
	[Export] public bool FlattenKnockback { get; private set; } = true;

	[ExportGroup("Stats")]
	/// <summary>
	/// Resistance to knockback forces. Resolved via the polymorphic
	/// <see cref="BaseFloatValueDefinition"/> family — assign a <see cref="ConstantFloatDefinition"/>
	/// for flat tuning, or an <see cref="AttributeFloatDefinition"/> to drive from an
	/// <see cref="IStatProvider"/> attribute. Null → 0 (no resistance baseline).
	///
	/// Resistance formula: <c>resistanceFactor = 1 / (1 + stability)</c>
	/// (0 = full force, 1 = half force, 3 = quarter force).
	/// </summary>
	[Export] public BaseFloatValueDefinition? Stability { get; private set; }

	/// <summary>
	/// Mass used to convert incoming impulse (N·s) into a velocity-delta (m/s). Assign a
	/// <see cref="ConstantFloatDefinition"/> for flat tuning or <see cref="AttributeFloatDefinition"/>
	/// for stat-driven mass. Null → 1.0 (preserves pre-mass-aware knockback feel).
	/// </summary>
	[Export] public BaseFloatValueDefinition? Mass { get; private set; }

	#endregion

	#region COMPONENT_UPDATES

	public override void _Ready()
	{
		base._Ready();
		// Component disables itself until Initialize() succeeds.
		ProcessMode = ProcessModeEnum.Disabled;
	}

	#endregion

	#region COMPONENT_LOGIC

	/// <summary>
	/// Universal force-carrier filter. Accepts any <see cref="CombatResult"/> implementing
	/// <see cref="IForceCarrier"/> with positive force — currently <c>DamageResult</c> and
	/// <c>KnockbackResult</c>, plus any future force-bearing result type that adds the marker.
	/// </summary>
	/// <remarks>
	/// <c>PreserveVertical</c> is read off <see cref="KnockbackResult"/> when present and forwarded
	/// to <see cref="ApplyKnockback"/> — producers like <c>KnockbackEffect</c> with
	/// <c>UpwardAngleDegrees</c> &gt; 0 (rising rock pillar) stamp this to signal Direction.Y
	/// is intentional. Other <see cref="IForceCarrier"/> types (DamageResult, future) default
	/// to false and continue to flatten when <see cref="FlattenKnockback"/> is true.
	/// </remarks>
	private void OnCombatResult(CombatResult result)
	{
		if (result is not IForceCarrier carrier || carrier.Force <= 0f) { return; }

		var preserveVertical = (result as KnockbackResult)?.PreserveVertical ?? false;
		ApplyKnockback(carrier.Direction, carrier.Force, result.Source, preserveVertical);
	}

	/// <summary>
	/// Applies a knockback impulse (CharacterBody regime: manual mass-division converts the
	/// incoming N·s impulse into an m/s velocity-delta).
	/// </summary>
	/// <param name="direction">Normalized direction of the knockback.</param>
	/// <param name="incomingForce">Impulse magnitude in N·s.</param>
	/// <param name="attributedSource">Originating cause for HSM transition / VFX / audio chain attribution.</param>
	/// <param name="preserveVertical">
	/// When true, the receiver's <see cref="FlattenKnockback"/> safety-net flatten is bypassed
	/// — the source has stamped Direction.Y as intentional (e.g., rock pillar's rising-pop).
	/// When false (default), FlattenKnockback (if true) zeros the Y component as before.
	/// </param>
	public void ApplyKnockback(Vector3 direction, float incomingForce, Node? attributedSource = null, bool preserveVertical = false)
	{
		if (!float.IsFinite(incomingForce) || incomingForce <= 0f)
		{
			JmoLogger.Warning(this, $"Knockback skipped: invalid force={incomingForce:F2}.");
			return;
		}
		var stability = Stability?.ResolveFloatValue(_statProvider) ?? 0f; // 0 = no resistance default
		var mass = Mass?.ResolveFloatValue(_statProvider) ?? 1f;           // 1.0 preserves existing feel
		if (mass <= 0f)
		{
			JmoLogger.Warning(this, $"Knockback skipped: invalid mass={mass:F2}. Set Mass > 0 in Inspector.");
			return;
		}

		var stabilityScaled = StabilityScaling.ScaleForce(direction * incomingForce, stability);
		// CharacterBody regime: input is N·s; manual mass-division to convert to velocity-delta (m/s).
		var velocityDelta = stabilityScaled / mass;
		if (FlattenKnockback && !preserveVertical)
		{
			velocityDelta = new Vector3(velocityDelta.X, 0f, velocityDelta.Z);
		}

		// MovementProcessor3D.ApplyImpulse expects m/s velocity-delta, not N·s impulse.
		_movementProcessor.ApplyImpulse(velocityDelta);
		EmitSignal(SignalName.KnockbackApplied, direction, velocityDelta.Length(), attributedSource);
		JmoLogger.Info(this, $"Knockback applied: dir={direction}, |Δv|={velocityDelta.Length():F2}");
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
		base._ExitTree();
	}

	#endregion

	#region INTERFACE_IMPLEMENTATION

	public bool IsInitialized { get; private set; }
	public event Action Initialized = delegate { };

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

		// Soft dep — null is acceptable. AttributeFloatDefinition.ResolveFloatValue handles null safely.
		bb.TryGet(BBDataSig.Stats, out _statProvider);

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
		// Dependencies are retrieved from Blackboard at runtime; no scene-time validation needed.
		return warnings.Concat(base._GetConfigurationWarnings() ?? []).ToArray();
	}

	#endregion

#if TOOLS
	#region Test Helpers

	// Logic-Domain tests construct the component without going through the BB wiring
	// pipeline (Initialize). These setters allow direct injection so the
	// FlattenKnockback × PreserveVertical × stat-driven gates can be exercised
	// without a full CombatantComponent + Blackboard fixture. Compiled out of release
	// builds via #if TOOLS.
	internal void SetMovementProcessorForTesting(IMovementProcessor3D processor) =>
		_movementProcessor = processor;
	internal void SetStability(BaseFloatValueDefinition? value) => Stability = value;
	internal void SetMass(BaseFloatValueDefinition? value) => Mass = value;
	internal void SetFlattenKnockback(bool value) => FlattenKnockback = value;

	#endregion
#endif
}
