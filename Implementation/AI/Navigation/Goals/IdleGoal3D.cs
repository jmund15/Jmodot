namespace Jmodot.Implementation.AI.Navigation.Goals;

using Core.AI.Navigation.Considerations;
using Godot;

/// <summary>
/// Picks no destination, never ends and contributes no steering, so the step's own style terms
/// move the agent. Host it in a MoveAction when a condition or the state, not the goal, ends the step.
/// </summary>
[GlobalClass, Tool]
public partial class IdleGoal3D : MovementGoal3D
{
    public override bool EndsItself => false;

    public override GoalStatus Advance(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
        => GoalStatus.Running;
}
