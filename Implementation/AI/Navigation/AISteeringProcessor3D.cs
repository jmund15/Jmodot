namespace Jmodot.AI.Navigation;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Implementation.AI.Navigation;
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

    [ExportGroup("Behavior")]
    /// <summary>
    /// If true, the final, blended direction vector will be snapped to the closest vector
    /// in the MovementDirections set. This is useful for grid-based or 8-way sprite movement.
    /// </summary>
    [Export] private bool _snapToDirectionSet = false;

    /// <summary>
    /// Considerations are sorted by priority to ensure a deterministic evaluation order, though
    /// in practice the order of addition does not change the final sum.
    /// </summary>
    public IOrderedEnumerable<BaseAIConsideration3D> SortedConsiderations { get; private set; }

    /// <summary>
    /// A dictionary to hold the aggregated scores for each direction during a single frame's calculation.
    /// </summary>
    private Dictionary<Vector3, float> _scores = new();

    /// <summary>
    /// The final, normalized direction vector calculated in the last frame. This can be used
    /// for debugging or for other systems to know the AI's current intent.
    /// </summary>
    public Vector3 DesiredDirection { get; private set; }

    [ExportGroup("Debug")] private bool _showNavigationDebugArrows;

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (_considerations.Count == 0) warnings.Add("No considerations are assigned. The AI will have no environmental awareness.");
        if (MovementDirections == null || !MovementDirections.Directions.Any()) warnings.Add("No movement directions are defined. The AI will not know how to score potential moves.");
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

        SortedConsiderations = _considerations.OrderBy(c => c.Priority);
    }

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
        foreach (var key in keys) _scores[key] = 0f;

        // --- 2. Evaluate all environmental considerations ---
        // Each consideration adds its own scores to the master dictionary.
        foreach (var consideration in SortedConsiderations)
        {
            consideration.Evaluate(context3D, blackboard, MovementDirections, ref _scores);
        }

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
        }
        else
        {
            DesiredDirection = _snapToDirectionSet
                ? MovementDirections.GetClosestDirection(finalDirection.Normalized())
                : finalDirection.Normalized();
        }

        return DesiredDirection;
    }
}
