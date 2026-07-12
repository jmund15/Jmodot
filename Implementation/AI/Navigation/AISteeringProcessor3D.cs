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
    /// The final, normalized direction vector calculated in the last frame. This can be used
    /// for debugging or for other systems to know the AI's current intent.
    /// </summary>
    public Vector3 DesiredDirection { get; private set; }

    /// <summary>
    /// Layer 1 override: When true, skips all consideration scoring (flee, wander, obstacle
    /// avoidance) but keeps nav path evaluation. The critter follows the nav agent's path
    /// to its target position. Navmesh geometry naturally handles obstacle avoidance.
    /// Use case: CorneredAction shuttle between close navmesh-valid waypoints.
    /// Single-owner: Only one BT action should set this at a time. Last writer wins —
    /// if multiple consumers are needed, replace with a push/pop counter.
    /// </summary>
    public bool NavigationOnlyMode { get; set; }

    /// <summary>
    /// Layer 2 override: When set, bypasses ALL steering computation (considerations, nav path,
    /// synthesis). Returns this raw direction as DesiredDirection. Strongest override.
    /// Priority: DirectionOverride > NavigationOnlyMode > Normal.
    /// Use case: Forced/scripted movement, cutscene movement, knockback effects.
    /// </summary>
    public Vector3? DirectionOverride { get; set; }

    /// <summary>
    /// Clears any active direction override, reverting to normal steering or NavigationOnlyMode.
    /// </summary>
    public void ClearDirectionOverride() => DirectionOverride = null;

    [ExportGroup("Synthesis")]
    /// <summary>
    /// The strategy that collapses the per-frame context map into a desired direction. Optional —
    /// null falls back to a lazily-created shared default <see cref="ArgmaxSynthesisStrategy3D"/>
    /// (one-time Info log). Deliberately NOT [RequiredExport]: this lands before the .tres is assigned
    /// on npc_template, and a required export would throw at _Ready for every existing NPC in that gap.
    /// </summary>
    [Export] private SteeringSynthesisStrategy3D? _synthesisStrategy;

    private StringName? _synthesisOverrideOwner;
    private SteeringSynthesisStrategy3D? _synthesisOverride;
    private SteeringSynthesisState _synthesisState = SteeringSynthesisState.Empty;

    // Lazily created; static so all unassigned processors share one instance. NEVER an eager field
    // initializer — Resource allocation at type-load would SIGSEGV pure-CLR tests.
    private static SteeringSynthesisStrategy3D? s_defaultStrategy;
    private static bool s_defaultLogged;

    /// <summary>
    /// Sets a BT-moment synthesis override under owned-slot discipline (reject-second-claimant).
    /// Returns false + warns when another owner holds the slot; on success resets synthesis state
    /// so the new strategy commits fresh.
    /// </summary>
    public bool TrySetSynthesisOverride(StringName owner, SteeringSynthesisStrategy3D strategy)
    {
        if (_synthesisOverrideOwner != null && _synthesisOverrideOwner != owner)
        {
            JmoLogger.Warning(this, $"[Steering] Synthesis override already held by '{_synthesisOverrideOwner}'; '{owner}' rejected.");
            return false;
        }
        _synthesisOverrideOwner = owner;
        _synthesisOverride = strategy;
        _synthesisState = SteeringSynthesisState.Empty;
        return true;
    }

    /// <summary>
    /// Clears the synthesis override. Owner-checked — a non-owner clear is a warned no-op. Resets
    /// synthesis state so the restored strategy commits fresh.
    /// </summary>
    public void ClearSynthesisOverride(StringName owner)
    {
        if (_synthesisOverrideOwner != owner)
        {
            JmoLogger.Warning(this, $"[Steering] '{owner}' tried to clear synthesis override held by '{_synthesisOverrideOwner}'.");
            return;
        }
        _synthesisOverrideOwner = null;
        _synthesisOverride = null;
        _synthesisState = SteeringSynthesisState.Empty;
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

    [ExportGroup("Debug")]
    [Export] private bool _showNavigationDebugArrows;

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
        // committed bin, override slot, or bypass flags. (Part 4 replaces the legacy
        // DirectionOverride/NavigationOnlyMode reset lines with a control-claim-slot reset.)
        _synthesisState = SteeringSynthesisState.Empty;
        _synthesisOverrideOwner = null;
        _synthesisOverride = null;
        _navPathOverride = null;
        DirectionOverride = null;
        NavigationOnlyMode = false;
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
    internal SteeringSynthesisStrategy3D GetActiveSynthesisStrategyForTest() => _synthesisOverride ?? _synthesisStrategy ?? GetOrCreateDefaultStrategy();
    internal int GetCommittedBinForTest() => _synthesisState.CommittedBin;
    internal StringName? GetSynthesisOverrideOwnerForTest() => _synthesisOverrideOwner;
#endif
    #endregion

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (!this._showNavigationDebugArrows)
        {
            return;
        }

        // HACK: change later?
        var _ownerAgent = GetOwner<Node3D>();
        var _time = Time.GetTicksMsec() / 1000.0f;
        var arrowSize = 4f;
        var arrowheadSize = 0.1f;


        for (int i = 0; i < this._map.Bins.Count; i++)
        {
            var weight = this._map.Interest[i] - this._map.Danger[i];
            var arrowColor = Colors.Yellow;
            if (weight < 0.2f)
            {
                weight = 0.2f;
                arrowColor = Colors.Red;
            }
            else if (weight > 0.5f)
            {
                arrowColor = Colors.Green;
            }
            arrowColor.A = 0.5f;
            var arrowPosition = new Vector3(_ownerAgent.GlobalPosition.X, _ownerAgent.GlobalPosition.Y + 1f, _ownerAgent.GlobalPosition.Z);

            var dirArrow = this._map.Bins[i] * weight * arrowSize;
            DebugDraw3D.DrawArrow(arrowPosition, arrowPosition + dirArrow,
                arrowColor,
                arrowheadSize,
                true);
        }

        var chosenDirArrow = this.DesiredDirection * 0.1f * arrowSize;
        chosenDirArrow.Y = 0;
        DebugDraw3D.DrawArrow(_ownerAgent.GlobalPosition, _ownerAgent.GlobalPosition + chosenDirArrow,
            Colors.Black,
            arrowheadSize,
            true);

        //DebugDraw3D.DrawLine(line_begin, line_end, new Color(1, 1, 0));
        DebugDraw2D.SetText("Time", _time);
        DebugDraw2D.SetText("Frames drawn", Engine.GetFramesDrawn());
        DebugDraw2D.SetText("FPS", Engine.GetFramesPerSecond());
        DebugDraw2D.SetText("delta", delta);
    }

    /// <summary>
    /// The main calculation method. It evaluates all considerations and combines them
    /// into a single, optimal steering vector.
    /// </summary>
    public Vector3 CalculateSteering(SteeringDecisionContext3D context3D, IBlackboard blackboard)
    {
        // --- Layer 2: DirectionOverride — complete bypass ---
        if (DirectionOverride.HasValue)
        {
            DesiredDirection = DirectionOverride.Value;
            return DesiredDirection;
        }

        // --- 1. Reset the steering map for this frame's calculation ---
        _map.Clear();

        // --- 2. Evaluate all environmental considerations ---
        // Layer 1: NavigationOnlyMode skips consideration scoring entirely.
        if (!NavigationOnlyMode)
        {
            foreach (var consideration in ActiveConsiderations)
            {
                consideration.Evaluate(context3D, blackboard, MovementDirections, _map);
            }
        }

        // --- 2b. Evaluate the dedicated nav path consideration ---
        // Evaluated in both normal and NavigationOnlyMode — nav path always active.
        var activeNavPath = _navPathOverride ?? _navPathConsideration;
        activeNavPath?.Evaluate(context3D, blackboard, MovementDirections, _map);

        // --- 3. Synthesize the final direction via the active strategy ---
        // Precedence: BT-moment override > Inspector-assigned strategy > lazily-created shared default.
        var strategy = _synthesisOverride ?? _synthesisStrategy ?? GetOrCreateDefaultStrategy();
        var (direction, newState) = strategy.Synthesize(_map, _synthesisState);
        _synthesisState = newState;

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

        return DesiredDirection;
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
