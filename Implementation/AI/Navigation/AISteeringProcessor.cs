#region

using GColl = Godot.Collections;

#endregion

namespace Jmodot.AI.Navigation;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Implementation.AI.Navigation;
using Implementation.Shared;

/// <summary>
///     The AI's low-level "brain" responsible for moment-to-moment steering. It synthesizes a
///     high-level goal (from a Behavior Tree) with a set of environmental considerations,
///     to produce a final, desired movement direction.
/// </summary>
[Tool]
[GlobalClass]
public partial class AISteeringProcessor : Node
{
    [Export] private GColl.Array<BaseAIConsideration3D> _considerations = new();
    private Node3D _ownerAgent = null!;

    private Dictionary<Vector3, float> _scores = new();

    [ExportGroup("Debug")] private bool _showNavigationDebugArrows;

    [ExportGroup("Configuration")] [Export]
    private bool _snapToDirectionSet;

    public IOrderedEnumerable<BaseAIConsideration3D> SortedConsiderations { get; private set; } = null!;

    /// <summary>
    ///     A list of normalized vectors representing the directions the AI can choose to move in (e.g., 8 or 16 directions).
    /// </summary>
    [Export]
    public DirectionSet3D MovementDirections { get; private set; } = null!;

    public Vector3 DesiredDirection { get; private set; }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (this._considerations.Count == 0)
            warnings.Add("No considerations are assigned. The AI will have no environmental awareness.");
        if (this.MovementDirections == null || !this.MovementDirections.Directions.Any())
            warnings.Add("No movement directions are defined. The AI will not know how to score potential moves.");
        return warnings.ToArray();
    }

    /// <summary>
    ///     Initializes the steering processor. Must be called by the parent AIAgent.
    /// </summary>
    public void Initialize()
    {
        // HACK: bad, fix later
        this._ownerAgent = this.GetOwner<Node3D>();

        if (this.MovementDirections == null || !this.MovementDirections.Directions.Any())
        {
            JmoLogger.Error(this,
                "MovementDirections array is null or empty. The steering processor cannot function.");
            return;
        }

        this._scores = this.MovementDirections.Directions.ToDictionary(dir => dir, dir => 0f);

        this.SortedConsiderations = this._considerations.OrderBy(consid => consid.Priority);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (!this._showNavigationDebugArrows) return;
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
            DebugDraw3D.DrawArrow(this._ownerAgent.GlobalPosition, this._ownerAgent.GlobalPosition + dirArrow,
                arrowColor,
                arrowheadSize,
                true);
        }

        var chosenDirArrow = this.DesiredDirection * 0.1f * arrowSize;
        chosenDirArrow.Y = 0;
        DebugDraw3D.DrawArrow(this._ownerAgent.GlobalPosition, this._ownerAgent.GlobalPosition + chosenDirArrow,
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
    ///     The main calculation method. It evaluates all considerations and combines them
    ///     into a single, optimal steering vector.
    /// </summary>
    public Vector3 CalculateSteering(DecisionContext context, IBlackboard blackboard)
    {
        if (this._scores == null) return Vector3.Zero; // Not initialized.

        // --- 1. Reset scores for this frame's calculation ---
        foreach (var key in this._scores.Keys.ToList()) this._scores[key] = 0f;

        // --- 2. Score the High-Level Goal ---
        if (!context.NextPathPointDirection.IsZeroApprox())
            foreach (var dir in this.MovementDirections.Directions)
            {
                var dot = dir.Dot(context.NextPathPointDirection);
                // Score is higher for directions that align with the target direction.
                if (dot > 0) this._scores[dir] += dot;
            }

        // --- 3. Score Environmental Considerations ---
        foreach (var consideration in this.SortedConsiderations)
        {
            if (consideration == null) continue;
            consideration.Evaluate(context, blackboard, this.MovementDirections, ref this._scores);
        }

        // --- 4. Choose the Best Direction ---
        var finalDirection = Vector3.Zero;
        foreach (var score in this._scores)
            // A direction's final score is clamped at 0. Negative scores (danger) cancel out
            // interest, but do not create a "desire" to move in the opposite direction.
            // Avoidance is simply the absence of interest in a given direction.
            finalDirection += score.Key * Mathf.Max(0, score.Value);
        this.DesiredDirection = finalDirection.IsZeroApprox() ? Vector3.Zero :
            this._snapToDirectionSet ? this.MovementDirections.GetClosestDirection(finalDirection.Normalized()) :
            finalDirection.Normalized();

        return this.DesiredDirection;
    }
}
