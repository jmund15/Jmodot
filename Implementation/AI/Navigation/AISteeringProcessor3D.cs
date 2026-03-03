namespace Jmodot.AI.Navigation;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Implementation.AI.Navigation;
using Implementation.AI.Navigation.Considerations;
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
    /// The order in the array does not matter; they will be sorted by their internal Priority property.
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
    /// Maximum turn speed in degrees per second. Controls how fast the steering output
    /// can change direction between frames (slerp-clamped on the XZ plane).
    /// 0 = unlimited (no smoothing, raw direction used). 180 = full U-turn in 1 second.
    /// Exposed as a public property for future ModifiableProperty stat wrapping.
    /// </summary>
    [Export(PropertyHint.Range, "0, 720, 1")]
    private float _maxTurnRateDegrees = 0f;

    /// <summary>
    /// Public accessor for turn rate. Allows owning entities to wrap this with
    /// ModifiableProperty for status effects (e.g., slow debuff reducing turn speed).
    /// </summary>
    public float MaxTurnRateDegrees
    {
        get => _maxTurnRateDegrees;
        set => _maxTurnRateDegrees = value;
    }

    /// <summary>
    /// Considerations are sorted by priority to ensure a deterministic evaluation order, though
    /// in practice the order of addition does not change the final sum.
    /// </summary>
    public IOrderedEnumerable<BaseAIConsideration3D> SortedConsiderations { get; private set; }

    /// <summary>
    /// Runtime considerations registered dynamically by BT actions.
    /// Kept separate from _considerations (Inspector-authored) to preserve editor data.
    /// </summary>
    private readonly List<BaseAIConsideration3D> _runtimeConsiderations = new();

    /// <summary>
    /// A dictionary to hold the aggregated scores for each direction during a single frame's calculation.
    /// </summary>
    private Dictionary<Vector3, float> _scores = new();

    /// <summary>
    /// Tracks the previous frame's output direction for turn rate smoothing.
    /// Reset to zero when direction goes to zero (idle).
    /// </summary>
    private Vector3 _previousDirection;

    /// <summary>
    /// The final, normalized direction vector calculated in the last frame. This can be used
    /// for debugging or for other systems to know the AI's current intent.
    /// </summary>
    public Vector3 DesiredDirection { get; private set; }

    [ExportGroup("Debug")] private bool _showNavigationDebugArrows;

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

        // Initialize the scores dictionary with a key for each available direction.
        _scores = MovementDirections.Directions.ToDictionary(dir => dir, dir => 0f);

        // Initialize each consideration, allowing them to perform setup tasks (e.g., caching data).
        foreach (var consideration in _considerations)
        {
            consideration?.Initialize(MovementDirections);
        }

        // Initialize the dedicated nav path consideration if configured.
        _navPathConsideration?.Initialize(MovementDirections);

        RebuildSortedConsiderations();
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
        RebuildSortedConsiderations();
    }

    /// <summary>
    /// Unregisters a previously registered runtime consideration.
    /// Used by BT actions to remove their steering behaviors when their task ends.
    /// </summary>
    public void UnregisterConsideration(BaseAIConsideration3D consideration)
    {
        if (!_runtimeConsiderations.Remove(consideration)) { return; }
        RebuildSortedConsiderations();
    }

    private void RebuildSortedConsiderations()
    {
        SortedConsiderations = _considerations
            .Concat(_runtimeConsiderations)
            .OrderBy(c => c.Priority);
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


        foreach (var dirWeight in this._scores)
        {
            var weight = dirWeight.Value;
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

            var dirArrow = dirWeight.Key * weight * arrowSize;
            DebugDraw3D.DrawArrow(_ownerAgent.GlobalPosition, _ownerAgent.GlobalPosition + dirArrow,
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
        // --- 1. Reset scores for this frame's calculation ---
        var keys = _scores.Keys.ToList();
        foreach (var key in keys) { _scores[key] = 0f; }

        // --- 2. Evaluate all environmental considerations ---
        // Each consideration adds its own scores to the master dictionary.
        foreach (var consideration in SortedConsiderations)
        {
            consideration.Evaluate(context3D, blackboard, MovementDirections, ref _scores);
        }

        // --- 2b. Evaluate the dedicated nav path consideration ---
        // Evaluated separately to enforce singleton semantics. Override takes precedence.
        var activeNavPath = _navPathOverride ?? _navPathConsideration;
        activeNavPath?.Evaluate(context3D, blackboard, MovementDirections, ref _scores);

        // --- 3. Synthesize the final direction ---
        // Combine all scored directions into a single resultant vector.
        Vector3 finalDirection = Vector3.Zero;
        foreach (var score in _scores)
        {
            // A direction's final score is clamped at 0. Negative scores (danger) cancel out
            // interest, but do not create a "desire" to move in the opposite direction.
            // Avoidance is simply the absence of interest in a given direction.
            finalDirection += score.Key * Mathf.Max(0, score.Value);
        }

        if (finalDirection.IsZeroApprox())
        {
            DesiredDirection = Vector3.Zero;
            // Don't update _previousDirection on idle — preserve last known heading
        }
        else
        {
            var normalized = _snapToDirectionSet
                ? MovementDirections.GetClosestDirection(finalDirection.Normalized())
                : finalDirection.Normalized();

            // Apply turn rate limiting if configured
            DesiredDirection = ApplyTurnRateLimit(
                _previousDirection, normalized, _maxTurnRateDegrees, context3D.PhysicsDelta);

            _previousDirection = DesiredDirection;
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
