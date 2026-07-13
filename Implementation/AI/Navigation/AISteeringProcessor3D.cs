namespace Jmodot.AI.Navigation;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Implementation.AI.Navigation;
using Implementation.AI.Navigation.Considerations;
using Implementation.AI.Navigation.Synthesis;
using Implementation.Shared;
using GColl = Godot.Collections;

/// <summary>
/// The active steering control-claim mode. Precedence when consumed: DirectionOverride beats
/// NavigationOnly beats Full. Full (the unclaimed default) runs the normal pipeline.
/// </summary>
public enum SteeringControlMode
{
    /// <summary>Normal steering: considerations + nav path + synthesis.</summary>
    Full,

    /// <summary>Skip consideration scoring; keep nav-path evaluation (navmesh-driven movement).</summary>
    NavigationOnly,

    /// <summary>Bypass all steering; return the claimed raw direction (scripted/knockback movement).</summary>
    DirectionOverride,
}

/// <summary>
///     The AI's tactical brainstem responsible for moment-to-moment steering. It synthesizes a
///     high-level goal (from a Behavior Tree) with a set of environmental considerations,
///     to produce a final, desired movement direction.
/// </summary>
[Tool]
[GlobalClass]
public partial class AISteeringProcessor3D : Node
{
    [ExportGroup("Configuration")]
    /// <summary>
    /// The set of discrete directions the AI uses to evaluate its world. All consideration
    /// scores will be calculated for the vectors in this set.
    /// </summary>
    [Export]
    public DirectionSet3D MovementDirections { get; private set; } = null!;

    /// <summary>
    /// The collection of all environmental considerations the AI will use to make decisions.
    /// Order is insertion order; the channel sum is order-independent so array order does not
    /// change the outcome, only the debug/eval evaluation order.
    /// </summary>
    [Export] private GColl.Array<BaseAIConsideration3D> _considerations = new();

    [ExportGroup("Navigation Path")]
    /// <summary>
    /// The processor-owned navigation path consideration. Evaluated separately from the
    /// regular considerations array to guarantee exactly one nav path consideration is active.
    /// When a nav path exists, this provides the primary "desire" to move toward the goal.
    /// BT actions can temporarily override this via <see cref="OverrideNavPathConsideration"/>.
    /// </summary>
    [Export] private NavigationPath3DConsideration? _navPathConsideration;

    /// <summary>
    /// A temporary override for the nav path consideration, set by BT actions that need
    /// custom path-following behavior (different weight, modifiers, or scoring algorithm).
    /// When non-null, this takes precedence over <see cref="_navPathConsideration"/>.
    /// </summary>
    private NavigationPath3DConsideration? _navPathOverride;

    [ExportGroup("Behavior")]
    /// <summary>
    /// If true, the final, blended direction vector will be snapped to the closest vector
    /// in the MovementDirections set. This is useful for grid-based or 8-way sprite movement.
    /// </summary>
    [Export] private bool _snapToDirectionSet = false;

    /// <summary>
    /// Static (Inspector-authored) considerations concatenated with runtime-registered ones in
    /// insertion order. Order is deterministic for debug/eval; the channel sum is order-independent.
    /// </summary>
    public IReadOnlyList<BaseAIConsideration3D> ActiveConsiderations { get; private set; }

    /// <summary>
    /// Runtime considerations registered dynamically by BT actions.
    /// Kept separate from _considerations (Inspector-authored) to preserve editor data.
    /// </summary>
    private readonly List<BaseAIConsideration3D> _runtimeConsiderations = new();

    /// <summary>
    /// The per-frame dual-channel steering map. Built from MovementDirections.OrderedDirections in
    /// Initialize; reset and re-populated each frame in CalculateSteering.
    /// </summary>
    private SteeringContextMap _map = null!;

    /// <summary>
    /// Optional per-consideration attribution recorder. Null until <see cref="EnableAttributionRecording"/>
    /// is called (by the debug overlay or the log dump). When non-null, CalculateSteering snapshots the
    /// map around each Evaluate call so each consideration's channel contribution is attributable.
    /// </summary>
    private DebugSteeringRecorder? _recorder;

    /// <summary>
    /// The final, normalized direction vector calculated in the last frame. This can be used
    /// for debugging or for other systems to know the AI's current intent.
    /// </summary>
    public Vector3 DesiredDirection { get; private set; }

    /// <summary>
    /// The active control-claim mode: DirectionOverride > NavigationOnly > Full (unclaimed default).
    /// Full runs the normal consideration + nav-path + synthesis pipeline; NavigationOnly skips
    /// consideration scoring but keeps nav-path evaluation; DirectionOverride bypasses all steering
    /// and returns the claimed raw direction. Claimed via <see cref="TryClaimControl"/> (reject-second-
    /// claimant), released via <see cref="ReleaseControl"/>.
    /// </summary>
    public SteeringControlMode ControlMode =>
        _controlSlot.IsClaimed ? _controlSlot.Value.Mode : SteeringControlMode.Full;

    /// <summary>The current control-claim owner, or null when unclaimed.</summary>
    public StringName? ControlOwner => _controlSlot.Owner;

    private readonly OwnedSlot<ControlClaim> _controlSlot = new();

    /// <summary>
    /// Claims the control slot under reject-second-claimant discipline: unclaimed or same-owner reclaim
    /// succeeds (mode updated); a different owner is rejected (returns false + warns). DirectionOverride
    /// requires a non-null direction — a null-direction DirectionOverride claim is rejected.
    /// Use cases: NavigationOnly for navmesh-only shuttle (CorneredAction, NavPursueAction);
    /// DirectionOverride for scripted/knockback movement.
    /// </summary>
    public bool TryClaimControl(StringName owner, SteeringControlMode mode, Vector3? direction = null)
    {
        if (mode == SteeringControlMode.DirectionOverride && direction == null)
        {
            JmoLogger.Warning(this, $"[Steering] '{owner}' claimed DirectionOverride with no direction; rejected.");
            return false;
        }
        return _controlSlot.TryClaim(owner, new ControlClaim(mode, direction), this, "Control");
    }

    /// <summary>Releases the control slot. Owner-checked — a non-owner release is a warned no-op.</summary>
    public void ReleaseControl(StringName owner) => _controlSlot.TryRelease(owner, this, "Control");

    /// <summary>The control slot's payload: the claimed mode and (for DirectionOverride) the raw direction.</summary>
    private readonly struct ControlClaim
    {
        public readonly SteeringControlMode Mode;
        public readonly Vector3? Direction;
        public ControlClaim(SteeringControlMode mode, Vector3? direction)
        {
            Mode = mode;
            Direction = direction;
        }
    }

    [ExportGroup("Synthesis")]
    /// <summary>
    /// The strategy that collapses the per-frame context map into a desired direction. Optional —
    /// null falls back to a lazily-created shared default <see cref="ArgmaxSynthesisStrategy3D"/>
    /// (one-time Info log). Deliberately NOT [RequiredExport]: this lands before the .tres is assigned
    /// on npc_template, and a required export would throw at _Ready for every existing NPC in that gap.
    /// </summary>
    [Export] private SteeringSynthesisStrategy3D? _synthesisStrategy;

    private readonly OwnedSlot<SteeringSynthesisStrategy3D> _synthesisSlot = new();
    private SteeringSynthesisState _synthesisState = SteeringSynthesisState.Empty;

    // Lazily created; static so all unassigned processors share one instance. NEVER an eager field
    // initializer — Resource allocation at type-load would SIGSEGV pure-CLR tests.
    private static SteeringSynthesisStrategy3D? s_defaultStrategy;
    private static bool s_defaultLogged;

    /// <summary>
    /// Sets a BT-moment synthesis override under the shared owned-slot discipline (reject-second-claimant).
    /// Returns false + warns when another owner holds the slot; on success resets synthesis state
    /// so the new strategy commits fresh.
    /// </summary>
    public bool TrySetSynthesisOverride(StringName owner, SteeringSynthesisStrategy3D strategy)
    {
        bool claimed = _synthesisSlot.TryClaim(owner, strategy, this, "Synthesis override");
        if (claimed) { _synthesisState = SteeringSynthesisState.Empty; }
        return claimed;
    }

    /// <summary>
    /// Clears the synthesis override. Owner-checked — a non-owner clear is a warned no-op. Resets
    /// synthesis state so the restored strategy commits fresh.
    /// </summary>
    public void ClearSynthesisOverride(StringName owner)
    {
        if (_synthesisSlot.TryRelease(owner, this, "Synthesis override"))
        {
            _synthesisState = SteeringSynthesisState.Empty;
        }
    }

    private SteeringSynthesisStrategy3D GetOrCreateDefaultStrategy()
    {
        if (_synthesisStrategy != null) { return _synthesisStrategy; }
        s_defaultStrategy ??= new ArgmaxSynthesisStrategy3D();
        if (!s_defaultLogged)
        {
            JmoLogger.Info(this, "[Steering] No synthesis strategy assigned; using shared default ArgmaxSynthesisStrategy3D.");
            s_defaultLogged = true;
        }
        return s_defaultStrategy;
    }

    /// <summary>
    /// Lazily creates (idempotent) and returns the shared per-consideration attribution recorder.
    /// Called by DebugSteeringComponent when its overlay toggle is on, and by Initialize when the
    /// log dump is enabled — both consumers read the same Contributions off one recorder.
    /// </summary>
    public DebugSteeringRecorder EnableAttributionRecording()
    {
        _recorder ??= new DebugSteeringRecorder();
        return _recorder;
    }

    /// <summary>
    /// One reject-second-claimant discipline, reused by the control slot and the synthesis-override
    /// slot: the first owner keeps the slot; a conflicting owner is rejected (returns false + warns);
    /// the owner (or a reset) releases it. Per-agent state lives here as a private field on the Node,
    /// never on a shared Resource.
    /// </summary>
    private sealed class OwnedSlot<T>
    {
        public StringName? Owner { get; private set; }
        public T? Value { get; private set; }
        public bool IsClaimed => Owner != null;

        public bool TryClaim(StringName owner, T value, Node context, string slotName)
        {
            if (Owner != null && Owner != owner)
            {
                JmoLogger.Warning(context, $"[Steering] {slotName} claim held by '{Owner}'; '{owner}' rejected.");
                return false;
            }
            Owner = owner;
            Value = value;
            return true;
        }

        public bool TryRelease(StringName owner, Node context, string slotName)
        {
            if (Owner != owner)
            {
                JmoLogger.Warning(context, $"[Steering] '{owner}' tried to release {slotName} held by '{Owner?.ToString() ?? "no one"}'.");
                return false;
            }
            Clear();
            return true;
        }

        public void Clear()
        {
            Owner = null;
            Value = default;
        }
    }

    [ExportGroup("Debug")]

    /// <summary>
    /// When true, emits a standing per-N-frame <c>[Steering]</c> attribution line via
    /// <see cref="JmoLogger.Debug"/> (chosen/committed bins + top-3 bins with the dominant
    /// contributor each). A permanent gate — NOT an ephemeral <c>[DIAG-*]</c> diagnostic.
    /// </summary>
    [Export] private bool _logSteeringAttribution;

    /// <summary>Frame interval between attribution log lines while <see cref="_logSteeringAttribution"/> is on.</summary>
    [Export(PropertyHint.Range, "1, 600, 1")] private int _logAttributionEveryNFrames = 30;

    private int _attributionFrameCounter;

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (_considerations.Count == 0) { warnings.Add("No considerations are assigned. The AI will have no environmental awareness."); }
        if (MovementDirections == null || !MovementDirections.Directions.Any()) { warnings.Add("No movement directions are defined. The AI will not know how to score potential moves."); }
        if (_navPathConsideration == null && GetParent()?.FindChild("AINavigator3D") != null)
        {
            warnings.Add("Parent has an AINavigator3D but no NavigationPath3DConsideration is set. Nav targets will be set but the agent will have no steering interest toward them.");
        }
        return warnings.ToArray();
    }

    /// <summary>
    ///     Initializes the steering processor. Must be called by the parent AIAgent.
    /// </summary>
    public void Initialize()
    {
        if (MovementDirections == null || !MovementDirections.Directions.Any())
        {
            JmoLogger.Error(this, "MovementDirections resource is null or empty. Steering processor cannot function.");
            return;
        }

        // Build the per-frame steering map over the ordered bin ring; carry the ring's circular-order
        // flag so synthesis strategies can gate neighbor interpolation on it.
        _map = new SteeringContextMap(MovementDirections.OrderedDirections, MovementDirections.HasCircularOrder);

        // Initialize each consideration, allowing them to perform setup tasks (e.g., caching data).
        foreach (var consideration in _considerations)
        {
            consideration?.Initialize(MovementDirections);
        }

        // Initialize the dedicated nav path consideration if configured.
        _navPathConsideration?.Initialize(MovementDirections);

        RebuildActiveConsiderations();

        // Reset hardening for pool reuse — a recycled processor must not carry a prior life's
        // committed bin, synthesis override, control claim, or nav-path override.
        _synthesisState = SteeringSynthesisState.Empty;
        _synthesisSlot.Clear();
        _controlSlot.Clear();
        _navPathOverride = null;

        // The standing log dump needs the recorder capturing; the overlay enables it independently.
        if (_logSteeringAttribution) { EnableAttributionRecording(); }
    }

    /// <summary>
    /// Registers a consideration at runtime. Used by BT actions to dynamically add
    /// steering behaviors (wander, flee, etc.) when their task becomes active.
    /// </summary>
    public void RegisterConsideration(BaseAIConsideration3D consideration)
    {
        if (_runtimeConsiderations.Contains(consideration)) { return; }
        _runtimeConsiderations.Add(consideration);
        consideration.Initialize(MovementDirections);
        RebuildActiveConsiderations();
    }

    /// <summary>
    /// Unregisters a previously registered runtime consideration.
    /// Used by BT actions to remove their steering behaviors when their task ends.
    /// </summary>
    public void UnregisterConsideration(BaseAIConsideration3D consideration)
    {
        if (!_runtimeConsiderations.Remove(consideration)) { return; }
        RebuildActiveConsiderations();
    }

    private void RebuildActiveConsiderations()
    {
        ActiveConsiderations = _considerations
            .Concat(_runtimeConsiderations)
            .ToList();
    }

    /// <summary>
    /// Temporarily overrides the processor's nav path consideration. Used by BT actions
    /// that need custom path-following behavior. Last writer wins — nested overrides
    /// are not stacked.
    /// </summary>
    public void OverrideNavPathConsideration(NavigationPath3DConsideration consideration)
    {
        _navPathOverride = consideration;
        consideration.Initialize(MovementDirections);
    }

    /// <summary>
    /// Clears any active nav path override, reverting to the processor's default
    /// <see cref="_navPathConsideration"/>.
    /// </summary>
    public void ClearNavPathOverride()
    {
        _navPathOverride = null;
    }

    #region Test Helpers
#if TOOLS
    internal void SetMovementDirections(DirectionSet3D directions) => MovementDirections = directions;
    internal void SetNavPathConsideration(NavigationPath3DConsideration? consideration) => _navPathConsideration = consideration;
    internal NavigationPath3DConsideration? GetActiveNavPathConsideration() => _navPathOverride ?? _navPathConsideration;
    internal void SetSynthesisStrategy(SteeringSynthesisStrategy3D? strategy) => _synthesisStrategy = strategy;
    internal SteeringSynthesisStrategy3D GetActiveSynthesisStrategyForTest() => _synthesisSlot.Value ?? _synthesisStrategy ?? GetOrCreateDefaultStrategy();
    internal int GetCommittedBinForTest() => _synthesisState.CommittedBin;
    internal StringName? GetSynthesisOverrideOwnerForTest() => _synthesisSlot.Owner;
    internal DebugSteeringRecorder? _TestRecorder => _recorder;
    internal void SetLogAttributionForTest(bool enabled, int everyN)
    {
        _logSteeringAttribution = enabled;
        _logAttributionEveryNFrames = everyN;
    }
#endif
    #endregion

    /// <summary>
    /// The main calculation method. It evaluates all considerations and combines them
    /// into a single, optimal steering vector.
    /// </summary>
    public Vector3 CalculateSteering(SteeringDecisionContext3D context3D, IBlackboard blackboard)
    {
        var mode = ControlMode;

        // --- DirectionOverride claim: complete bypass (direction guaranteed non-null by the claim guard) ---
        if (mode == SteeringControlMode.DirectionOverride)
        {
            // A DirectionOverride bypass produces no attribution; clear last frame's so the overlay/log
            // don't misrepresent a bypassed frame as live steering.
            _recorder?.BeginFrame(_map);
            DesiredDirection = _controlSlot.Value.Direction ?? Vector3.Zero;
            return DesiredDirection;
        }

        // --- 1. Reset the steering map for this frame's calculation ---
        _map.Clear();
        _recorder?.BeginFrame(_map);

        // --- 2. Evaluate all environmental considerations ---
        // NavigationOnly claim skips consideration scoring entirely.
        if (mode != SteeringControlMode.NavigationOnly)
        {
            foreach (var consideration in ActiveConsiderations)
            {
                _recorder?.CaptureBefore(_map);
                consideration.Evaluate(context3D, blackboard, MovementDirections, _map);
                _recorder?.CaptureAfter(consideration, _map);
            }
        }

        // --- 2b. Evaluate the dedicated nav path consideration ---
        // Evaluated in both Full and NavigationOnly modes — nav path always active.
        var activeNavPath = _navPathOverride ?? _navPathConsideration;
        if (activeNavPath != null)
        {
            _recorder?.CaptureBefore(_map);
            activeNavPath.Evaluate(context3D, blackboard, MovementDirections, _map);
            _recorder?.CaptureAfter(activeNavPath, _map);
        }

        // --- 3. Synthesize the final direction via the active strategy ---
        // Precedence: BT-moment override > Inspector-assigned strategy > lazily-created shared default.
        var strategy = _synthesisSlot.Value ?? _synthesisStrategy ?? GetOrCreateDefaultStrategy();
        var (direction, newState) = strategy.Synthesize(_map, _synthesisState);
        _synthesisState = newState;
        _recorder?.RecordDecision(_map, newState.CommittedBin, strategy.DangerScale);

        if (direction.IsZeroApprox())
        {
            DesiredDirection = Vector3.Zero;
        }
        else
        {
            DesiredDirection = _snapToDirectionSet
                ? MovementDirections.GetClosestDirection(direction.Normalized())
                : direction.Normalized();
        }

        LogAttributionIfDue();
        return DesiredDirection;
    }

    /// <summary>
    /// Standing per-N-frame attribution dump. No-op unless <see cref="_logSteeringAttribution"/> is on
    /// and a recorder is active. Reads the recorder populated during this frame's CalculateSteering.
    /// </summary>
    private void LogAttributionIfDue()
    {
        if (!_logSteeringAttribution || _recorder == null) { return; }
        if (++_attributionFrameCounter < _logAttributionEveryNFrames) { return; }
        _attributionFrameCounter = 0;

        var top = _recorder.GetTopBins(3);
        var sb = new System.Text.StringBuilder();
        sb.Append("[Steering] chosen=").Append(_recorder.ChosenBin)
          .Append(" committed=").Append(_recorder.CommittedBin).Append(" top=[");
        for (int t = 0; t < top.Count; t++)
        {
            if (t > 0) { sb.Append(", "); }
            int bin = top[t];
            sb.Append(bin).Append(':').Append(DominantContributor(bin));
        }
        sb.Append(']');
        JmoLogger.Debug(this, sb.ToString());
    }

    /// <summary>The consideration with the largest net (interest−danger) magnitude at a bin, for the log dump.</summary>
    private string DominantContributor(int bin)
    {
        string best = "-";
        float bestMag = 0f;
        foreach (var c in _recorder!.Contributions)
        {
            float mag = c.InterestDelta[bin] - c.DangerDelta[bin];
            if (Mathf.Abs(mag) > Mathf.Abs(bestMag)) { bestMag = mag; best = c.Source; }
        }
        return best;
    }

    /// <summary>
    /// Limits the rotation from previous to desired direction by a maximum angular speed.
    /// Operates on the XZ plane — Y components are flattened to zero.
    /// Returns the desired direction directly when: rate is 0 (unlimited), previous is zero
    /// (first frame), desired is zero (idle), or the angle is within the allowed rotation.
    /// Exposed as static for testability.
    /// </summary>
    public static Vector3 ApplyTurnRateLimit(
        Vector3 previous, Vector3 desired, float maxTurnRateDegrees, float delta)
    {
        // No smoothing
        if (maxTurnRateDegrees <= 0f || delta <= 0f)
        {
            return desired;
        }

        // Idle → return zero
        if (desired.IsZeroApprox())
        {
            return Vector3.Zero;
        }

        // First frame / coming from idle → snap to desired
        if (previous.IsZeroApprox())
        {
            return desired;
        }

        // Flatten to XZ plane for ground-based rotation
        Vector3 flatPrev = new Vector3(previous.X, 0, previous.Z);
        Vector3 flatDesired = new Vector3(desired.X, 0, desired.Z);

        if (flatPrev.LengthSquared() < 0.001f || flatDesired.LengthSquared() < 0.001f)
        {
            return desired;
        }

        flatPrev = flatPrev.Normalized();
        flatDesired = flatDesired.Normalized();

        float angleRad = flatPrev.AngleTo(flatDesired);

        // Already aligned
        if (angleRad < 0.001f)
        {
            return flatDesired;
        }

        float maxRadians = Mathf.DegToRad(maxTurnRateDegrees) * delta;

        // Can reach desired this frame
        if (angleRad <= maxRadians)
        {
            return flatDesired;
        }

        // Near-antiparallel: Slerp is degenerate when vectors are ~180° apart.
        // Manually rotate around the Y axis by the clamped angle.
        if (angleRad > Mathf.Pi - 0.01f)
        {
            // Choose perpendicular direction on XZ plane (rotate 90° around Y)
            Vector3 perp = new Vector3(-flatPrev.Z, 0, flatPrev.X).Normalized();
            // Use cross product to pick the side closer to desired
            float cross = flatPrev.X * flatDesired.Z - flatPrev.Z * flatDesired.X;
            if (cross < 0) { perp = -perp; }
            // Rotate from previous toward perp by maxRadians
            float t = maxRadians / (Mathf.Pi / 2f); // fraction of 90° toward perp
            if (t >= 1f) { return perp; }
            return flatPrev.Slerp(perp, t).Normalized();
        }

        // Slerp by the clamped fraction
        float sT = maxRadians / angleRad;
        Vector3 result = flatPrev.Slerp(flatDesired, sT);

        return result.Normalized();
    }
}
