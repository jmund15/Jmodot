namespace Jmodot.Implementation.AI.Navigation.Goals;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Godot;

/// <summary>
/// Internal bridge that lets a goal steer through the processor's consideration pipeline: one
/// term per action, bound to that action's goal, weighted by the goal, and named after it so
/// attribution and the log dump credit the goal. Not an authored Resource and never in a picker.
/// </summary>
internal sealed partial class GoalSteeringTerm : BaseAIConsideration3D
{
    private MovementGoal3D? _goal;

    /// <summary>Points this term at <paramref name="goal"/>, taking its weight and name.</summary>
    public void Bind(MovementGoal3D goal)
    {
        _goal = goal;
        Weight = goal.SteeringWeight;
        ResourceName = string.IsNullOrEmpty(goal.ResourceName)
            ? goal.GetType().Name
            : goal.ResourceName;
    }

    public override AIConsiderationRuntime? CreateRuntime(IBlackboard? blackboard)
        => _goal?.CreateRuntime(blackboard);

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard,
        AIConsiderationRuntime? runtime)
    {
        if (_goal == null) { return directions.Directions.ToDictionary(dir => dir, _ => 0f); }
        return _goal.Score(directions, context3D, blackboard, runtime);
    }
}
