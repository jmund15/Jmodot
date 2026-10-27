using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Actors;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Core.Stats;
using Jmodot.Core.Health;
using Jmodot.Core.Visual.Animation.Sprite;
using Jmodot.Implementation.Actors;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

namespace Jmodot.Implementation.Combat;

/// <summary>
/// Handles knockback application for CharacterBody3D entities. Subscribes to
/// <see cref="CombatantComponent.CombatResultEvent"/> and delivers resolved pushes through
/// <see cref="IMovementProcessor3D"/>, optionally freezing the victim before launch.
///
/// REQUIRED BLACKBOARD DEPENDENCIES:
/// - BBDataSig.MovementProcessor (IMovementProcessor3D) — applies the impulse.
/// - BBDataSig.CombatantComponent (CombatantComponent) — source of combat events.
///
/// OPTIONAL BLACKBOARD DEPENDENCIES:
/// - BBDataSig.Stats (IStatProvider) — feeds <see cref="AttributeFloatDefinition"/> resolution
///   for <see cref="Stability"/> / <see cref="Mass"/>. ConstantFloatDefinition users sidestep this.
/// - BBDataSig.CombatLog (CombatLog) — receives a <see cref="KnockbackAppliedResult"/> entry every
///   time <see cref="ApplyKnockback"/> succeeds, so HSM transition conditions
///   (<see cref="Jmodot.Examples.AI.HSM.TransitionConditions.KnockbackCondition"/>) can gate launch/stagger/ragdoll states off post-resistance
///   velocity-delta magnitude. Soft dep — null is acceptable; consumers without HSM
///   routing simply skip the audit-log write.
/// - BBDataSig.HealthComponent (IHealth) — a dead entity starts no hold; death during a hold
///   releases it and applies the pending push.
/// - BBDataSig.AnimationOrchestrator (IAnimationOrchestrator) — frozen for the hold's duration;
///   absent, only the drive freezes.
/// - <see cref="Hold"/>'s SuppressWhileFlagKey (bool) — read each physics tick while holding;
///   true prevents or ends a hold.
/// </summary>
[GlobalClass, Tool]
public partial class KnockbackComponent3D : Node3D, IComponent, IBlackboardProvider, IKnockbackReceiver3D
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
	private CombatLog? _combatLog;        // Soft dep — null is acceptable for HSM-less receivers.

	#endregion

	#region COMPONENT_VARIABLES

	/// <summary>
	/// When true this receiver keeps the Y component of the resulting velocity-delta. Default
	/// false zeroes it — the safety net against sloppy producers, typical for grounded characters
	/// that should be pushed horizontally, not lifted.
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
	/// <see cref="BaseFloatValueDefinition"/> family — assign a <see cref="ConstantFloatDefinition"/>
	/// for flat tuning, or an <see cref="AttributeFloatDefinition"/> to drive from an
	/// <see cref="IStatProvider"/> attribute. Null → 0 (no resistance baseline).
	///
	/// Resistance formula: <c>resistanceFactor = 1 / (1 + stability)</c>
	/// (0 = full force, 1 = half force, 3 = quarter force).
	/// </summary>
	[ExportGroup("Stats")]
	[Export] public BaseFloatValueDefinition? Stability { get; private set; }

	/// <summary>
	/// Mass used to convert incoming impulse (N·s) into a velocity-delta (m/s). Assign a
	/// <see cref="ConstantFloatDefinition"/> for flat tuning or <see cref="AttributeFloatDefinition"/>
	/// for stat-driven mass. Null → 1.0.
	/// </summary>
	[Export] public BaseFloatValueDefinition? Mass { get; private set; }

	/// <summary>Pushes closer together than this are damped by <see cref="RepeatFactor"/>. 0 = never.</summary>
	[ExportGroup("Repeat Hits")]
	[Export(PropertyHint.Range, "0,2,0.05,suffix:s")] public float RepeatWindowSeconds { get; private set; }

	/// <summary>Force multiplier for a push inside the window (first-hit-strong). 1 = no damping; valid range (0, 1].</summary>
	[Export(PropertyHint.Range, "0.05,1,0.05")] public float RepeatFactor { get; private set; } = 1f;

	/// <summary>When true a push also lifts the body a little, in proportion to its horizontal impulse.</summary>
	[ExportGroup("Vertical Pop")]
	[Export] public bool VerticalPop { get; private set; }

	/// <summary>Share of the horizontal velocity-delta added as lift.</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float PopFactor { get; private set; } = 0.25f;

	/// <summary>Cap on the lift speed (m/s), whatever the push or the mass.</summary>
	[Export(PropertyHint.Range, "0,10,0.1,suffix:m/s")] public float PopMaxSpeed { get; private set; } = 1.5f;

	private KnockbackHoldConfig? _hold;
	/// <summary>Optional victim-only pause before launch. Empty applies pushes immediately without freezing.</summary>
	[Export] public KnockbackHoldConfig? Hold
	{
		get => _hold;
		private set
		{
			if (ResourceExts.SetForwardedSlot(ref _hold, value, HoldForward())) { RefreshHoldWarnings(); }
		}
	}
	/// <summary>True while this receiver owns a pending push and movement suspension.</summary>
	public bool IsHolding { get; private set; }
	/// <summary>
	/// True while a push is held, and after its release until this receiver's next physics tick: movement
	/// queues the released impulse and applies it on its own tick, so velocity shows it only then.
	/// </summary>
	public bool IsPushPending => IsHolding || _releaseLanding;
	private bool _releaseLanding;
	/// <summary>
	/// Evidence window of a held push, including its release latency, while the push is held and for
	/// <see cref="ReleaseMarginSeconds"/> after it releases; zero otherwise.
	/// </summary>
	public float SourceLookupSeconds
		=> IsHolding || _unscaledSeconds() <= _windowClosesAt ? _sourceLookupSeconds : 0f;
	private float _sourceLookupSeconds;
	private double _windowClosesAt = double.NegativeInfinity;
	/// <summary>Seconds a released hold keeps its evidence window open, so the state machine reacting to the release still finds the push.</summary>
	public const float ReleaseMarginSeconds = 0.25f;
	private IHealth? _health;
	private IAnimationOrchestrator? _orchestrator;
	private IBlackboard? _blackboard;
	private bool _warnedSuppressionType;
	private HoldState _held = new();

	private struct HoldState
	{
		public Vector3 Impulse;
		public Vector3 Delta;
		public Node? Source;
		public double Deadline;
		public double MaxDeadline;
		public StringName Owner;

		public void Start(Vector3 impulse, Vector3 delta, Node? source, double deadline, double maxDeadline, StringName owner)
		{
			Impulse = impulse;
			Delta = delta;
			Source = source;
			Deadline = deadline;
			MaxDeadline = maxDeadline;
			Owner = owner;
		}

		public void Add(Vector3 impulse, Vector3 delta)
		{
			Impulse += impulse;
			Delta += delta;
		}

		public void Extend(Node? source, double deadline)
		{
			Source = source;
			Deadline = Math.Min(MaxDeadline, Math.Max(Deadline, deadline));
		}

		public void Clear() => Start(Vector3.Zero, Vector3.Zero, null, 0, 0, "");
	}

	private Callable HoldForward() => new(this, MethodName.OnHoldChanged);

	private void OnHoldChanged() => RefreshHoldWarnings();

	private void RefreshHoldWarnings()
	{
		if (Engine.IsEditorHint() && IsInsideTree()) { UpdateConfigurationWarnings(); }
	}

	// Unaffected by hitstop's time scale; double.NegativeInfinity stamp makes the first push ever full.
	private double _lastAppliedAt = double.NegativeInfinity;
	private Func<double> _unscaledSeconds = static () => Time.GetTicksMsec() / 1000.0;

	#endregion

	#region COMPONENT_UPDATES

	public override void _Ready()
	{
		base._Ready();
		SetPhysicsProcess(false);
		if (Engine.IsEditorHint()) { return; }
		// Component disables itself until Initialize() succeeds.
		ProcessMode = ProcessModeEnum.Disabled;
	}

	#endregion

	#region COMPONENT_LOGIC

	/// <summary>
	/// Universal force-carrier filter. Accepts any <see cref="CombatResult"/> implementing
	/// <see cref="IForceCarrier"/> with positive force — currently <see cref="DamageResult"/> and
	/// <see cref="KnockbackResult"/>, plus any future force-bearing result type that adds the marker.
	/// </summary>
	/// <remarks>
	/// <c>PreserveVertical</c> is read off <see cref="KnockbackResult"/> when present and forwarded
	/// to <see cref="ApplyKnockback"/> — producers like <see cref="Jmodot.Implementation.Combat.Effects.KnockbackEffect"/> with
	/// <c>UpwardAngleDegrees</c> &gt; 0 (rising rock pillar) stamp this to signal Direction.Y
	/// is intentional. Other <see cref="IForceCarrier"/> types (DamageResult, future) default
	/// to false and continue to flatten unless <see cref="PreserveVertical"/> is set on the receiver.
	/// </remarks>
	private void OnCombatResult(CombatResult result)
	{
		if (result is not IForceCarrier carrier || carrier.Force <= 0f) { return; }

		var preserveVertical = (result as KnockbackResult)?.PreserveVertical ?? false;
		Apply(carrier.Direction, carrier.Force, result.Source, preserveVertical, result.Sustained);
	}

	/// <summary>
	/// Applies a knockback impulse (CharacterBody regime: manual mass-division converts the
	/// incoming N·s impulse into an m/s velocity-delta).
	/// </summary>
	/// <param name="direction">Normalized direction of the knockback.</param>
	/// <param name="incomingForce">Impulse magnitude in N·s.</param>
	/// <param name="attributedSource">Originating cause for HSM transition / VFX / audio chain attribution.</param>
	/// <param name="preserveVertical">
	/// When true, the safety-net flatten is bypassed — the source has stamped Direction.Y as
	/// intentional (e.g., rock pillar's rising-pop). ORed with the receiver's own
	/// <see cref="PreserveVertical"/>: false here still preserves Y if the receiver asks for it.
	/// </param>
	public void ApplyKnockback(Vector3 direction, float incomingForce, Node? attributedSource = null, bool preserveVertical = false)
		=> Apply(direction, incomingForce, attributedSource, preserveVertical, sustained: false);

	private void Apply(Vector3 direction, float incomingForce, Node? attributedSource, bool preserveVertical, bool sustained)
	{
		if (!float.IsFinite(incomingForce) || incomingForce <= 0f)
		{
			JmoLogger.Warning(this, $"Knockback skipped: invalid force={incomingForce:F2}.");
			return;
		}
		if (!IsHolding && _movementProcessor.IsSuspended) { return; }
		var stability = Stability?.ResolveFloatValue(_statProvider) ?? 0f; // 0 = no resistance default
		var mass = Mass?.ResolveFloatValue(_statProvider) ?? 1f;           // 1.0 preserves existing feel
		if (mass <= 0f)
		{
			JmoLogger.Warning(this, $"Knockback skipped: invalid mass={mass:F2}. Set Mass > 0 in Inspector.");
			return;
		}

		var now = _unscaledSeconds();
		var shaping = new KnockbackShaping(
			Damping: sustained ? 1f : KnockbackPolicy.DampingFactor(now - _lastAppliedAt, RepeatWindowSeconds, RepeatFactor),
			PopRatio: VerticalPop ? PopFactor : 0f,
			PopMaxImpulse: PopMaxSpeed * mass);
		if (!sustained) { _lastAppliedAt = now; }
		var resolved = KnockbackPolicy.Resolve(direction, incomingForce, stability, PreserveVertical || preserveVertical, shaping);
		// CharacterBody regime: input is N·s; manual mass-division to convert to velocity-delta (m/s).
		var velocityDelta = resolved.Impulse / mass;

		var deltaMagnitude = velocityDelta.Length();
		KnockbackPolicy.LogApplied(_combatLog, this, attributedSource, resolved.AppliedDirection, deltaMagnitude, sustained);
		if (IsHolding)
		{
			_held.Add(resolved.Impulse, velocityDelta);
			if (!sustained)
			{
				float extension = Hold == null ? 0f : KnockbackHold.Seconds(_held.Impulse.Length(), _held.Delta.Length(), Hold);
				_held.Extend(attributedSource, now + extension);
			}
			return;
		}
		_sourceLookupSeconds = 0f;
		float seconds = Hold == null ? 0f : KnockbackHold.Seconds(resolved.Impulse.Length(), deltaMagnitude, Hold);
		if (!sustained && !IsDead && !IsSuppressed() && float.IsFinite(seconds) && seconds > 0f && double.IsFinite(now)
			&& _movementProcessor.TryClaimSuspension(Name, SuspensionVelocityPolicy.Zero))
		{
			_sourceLookupSeconds = Hold!.MaxSeconds + ReleaseMarginSeconds;
			_held.Start(resolved.Impulse, velocityDelta, attributedSource, now + seconds, now + Hold.MaxSeconds, Name);
			IsHolding = true;
			SetPhysicsProcess(true);
			SetSpriteFrozen(true);
			return;
		}

		_movementProcessor.ApplyImpulse(velocityDelta);
		EmitSignal(SignalName.KnockbackApplied, resolved.AppliedDirection, deltaMagnitude, ValidSource(attributedSource));

		JmoLogger.Debug(this, $"[Impact] Knockback applied: dir={resolved.AppliedDirection}, |Δv|={deltaMagnitude:F2}");
	}

	/// <summary>
	/// Resolves the entity's knockback receiver from <paramref name="bb"/> and reports whether it is
	/// inside a held push's evidence window — still holding, or released with the window retained.
	/// The one lookup every hold-aware consumer shares.
	/// </summary>
	/// <param name="seconds">The receiver's evidence window in seconds; 0 when there is none.</param>
	/// <param name="holding">The receiver's <see cref="IsPushPending"/>: held, or released and not yet applied by movement.</param>
	/// <returns>False when no valid receiver carries a positive window; the outputs are then zero/false.</returns>
	public static bool TryGetHeldPushWindow(IBlackboard? bb, out float seconds, out bool holding)
	{
		seconds = 0f;
		holding = false;
		if (bb == null
			|| !bb.TryGet<KnockbackComponent3D>(BBDataSig.KnockbackComponent, out var receiver)
			|| receiver == null
			|| !GodotObject.IsInstanceValid(receiver)
			|| !(receiver.SourceLookupSeconds > 0f))
		{
			return false;
		}

		seconds = receiver.SourceLookupSeconds;
		holding = receiver.IsPushPending;
		return true;
	}

	private static bool IsAlive(object? dependency) => dependency != null
		&& (dependency is not GodotObject godot || GodotObject.IsInstanceValid(godot));
	private bool IsDead => IsAlive(_health) && _health!.IsDead;
	private static Node? ValidSource(Node? source) => IsAlive(source) ? source : null;

	private bool IsSuppressed()
	{
		var key = Hold?.SuppressWhileFlagKey;
		if (key == null || key.IsEmpty || !IsAlive(_blackboard)) { return false; }
		if (_blackboard!.TryGet<bool>(key, out var suppressed)) { return suppressed; }
		if (!_warnedSuppressionType && _blackboard.ContainsLocal(key))
		{
			_warnedSuppressionType = true;
			JmoLogger.Warning(this, $"SuppressWhileFlagKey '{key}' is not a bool; suppression reads false.");
		}
		return false;
	}

	private void SetSpriteFrozen(bool frozen)
	{
		if (IsAlive(_orchestrator)) { _orchestrator!.SetFrozen(frozen); }
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint()) { return; }
		if (_releaseLanding)
		{
			_releaseLanding = false;
			SetPhysicsProcess(IsHolding);
		}
		if (!IsHolding) { return; }
		double remaining = _held.Deadline - _unscaledSeconds();
		if (!double.IsFinite(remaining) || !(remaining > 0) || IsSuppressed()) { ReleaseHold(); }
	}

	private void ReleaseHold(bool exiting = false)
	{
		if (!IsHolding) { return; }
		var pending = _held.Delta;
		var source = _held.Source;
		var owner = _held.Owner;
		IsHolding = false;
		_windowClosesAt = _unscaledSeconds() + ReleaseMarginSeconds;
		_held.Clear();
		_releaseLanding = !exiting;
		SetPhysicsProcess(_releaseLanding);
		if (IsAlive(_movementProcessor)) { _movementProcessor.ReleaseSuspension(owner); }
		SetSpriteFrozen(false);
		if (exiting)
		{
			Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(this) && IsInsideTree()) { LaunchPending(pending, source); }
			}).CallDeferred();
			return;
		}
		LaunchPending(pending, source);
	}

	private void LaunchPending(Vector3 pending, Node? source)
	{
		if (!IsAlive(_movementProcessor)) { return; }
		_movementProcessor.ApplyImpulse(pending);
		EmitSignal(SignalName.KnockbackApplied, pending.Normalized(), pending.Length(), ValidSource(source));
	}

	private void OnDied(HealthChangeEventArgs args) => ReleaseHold();

	private void SubscribeHealth()
	{
		if (!IsAlive(_health)) { return; }
		_health!.OnDied -= OnDied;
		_health.OnDied += OnDied;
	}

	public override void _EnterTree()
	{
		base._EnterTree();
		if (Engine.IsEditorHint()) { return; }
		// A reparent unsubscribes in _ExitTree and Phase 2 does not run again, so a built component re-arms here.
		if (IsInitialized)
		{
			_combatant.CombatResultEvent -= OnCombatResult;
			_combatant.CombatResultEvent += OnCombatResult;
			SubscribeHealth();
		}
	}

	public override void _ExitTree()
	{
		if (Engine.IsEditorHint()) { return; }
		ReleaseHold(exiting: true);
		if (IsAlive(_health)) { _health!.OnDied -= OnDied; }
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
		if (Engine.IsEditorHint()) { return false; }
		_blackboard = bb;
		bb.TryGet(BBDataSig.HealthComponent, out _health);
		bb.TryGet(BBDataSig.AnimationOrchestrator, out _orchestrator);
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

		// Soft dep — null is acceptable. Receivers without HSM transition conditions (e.g., barrels,
		// destructibles) won't have a CombatLog attached; the audit-log write becomes a no-op.
		bb.TryGet(BBDataSig.CombatLog, out _combatLog);

		IsInitialized = true;
		Initialized();
		return true;
	}

	public void OnPostInitialize()
	{
		if (Engine.IsEditorHint()) { return; }
		ProcessMode = ProcessModeEnum.Inherit;
		_combatant.CombatResultEvent -= OnCombatResult;
		_combatant.CombatResultEvent += OnCombatResult;
		SubscribeHealth();
	}

	public Node GetUnderlyingNode() => this;

	#endregion

	#region CONFIGURATION_WARNINGS

	public override string[] _GetConfigurationWarnings()
	{
		var warnings = new List<string>();
		warnings.AddRange(ConfigWarnings.ResourceSlotWarnings("Hold", Hold, required: false));
		// Dependencies are retrieved from Blackboard at runtime; no scene-time validation needed.
		if (RepeatFactor is <= 0f or > 1f) { warnings.Add("RepeatFactor must be in (0, 1]."); }
		if (PopMaxSpeed < 0f) { warnings.Add("PopMaxSpeed must be >= 0."); }
		return warnings.Concat(base._GetConfigurationWarnings() ?? []).ToArray();
	}

	#endregion

#if TOOLS
	#region Test Helpers

	// Logic-Domain tests construct the component without going through the BB wiring
	// pipeline (Initialize). These setters allow direct injection so the
	// receiver-side × producer-side PreserveVertical × stat-driven gates can be exercised
	// without a full CombatantComponent + Blackboard fixture. Compiled out of release
	// builds via #if TOOLS.
	internal void SetHealthForTesting(IHealth? value) => _health = value;
	internal void SetBlackboardForTesting(IBlackboard value) => _blackboard = value;
	internal void SetHoldDeadlineForTest_BypassEvents(double unscaledDeadline) => _held.Deadline = unscaledDeadline;
	internal void SetHoldForTesting(KnockbackHoldConfig? value) => Hold = value;
	internal void SetOrchestratorForTesting(IAnimationOrchestrator? value) => _orchestrator = value;
	internal void SetMovementProcessorForTesting(IMovementProcessor3D processor) =>
		_movementProcessor = processor;
	internal void SetStability(BaseFloatValueDefinition? value) => Stability = value;
	internal void SetMass(BaseFloatValueDefinition? value) => Mass = value;
	internal void SetPreserveVertical(bool value) => PreserveVertical = value;
	internal void SetShapingForTesting(float windowSeconds, float repeatFactor, bool verticalPop, float popFactor, float popMaxSpeed, Func<double> unscaledSeconds)
	{
		RepeatWindowSeconds = windowSeconds;
		RepeatFactor = repeatFactor;
		VerticalPop = verticalPop;
		PopFactor = popFactor;
		PopMaxSpeed = popMaxSpeed;
		_unscaledSeconds = unscaledSeconds;
	}

	#endregion
#endif
}
