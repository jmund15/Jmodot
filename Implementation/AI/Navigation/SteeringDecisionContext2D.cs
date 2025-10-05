namespace Jmodot.Implementation.AI.Navigation;

using Perception;

/// <summary>
///     An immutable, high-performance struct that provides a snapshot of all necessary world
///     and agent state for a single decision-making frame. It is created by the AIAgent
///     and passed to the AISteeringProcessor to ensure all considerations operate on the
///     same consistent data set.
/// </summary>
public readonly struct SteeringDecisionContext2D
{
    public readonly AIPerceptionManager2D Memory;
    public readonly Vector2 AgentPosition;
    public readonly Vector2 AgentFacingDirection;
    public readonly Vector2 AgentVelocity;
    public readonly Vector2 NextPathPointDirection;
    public readonly Vector2 TargetPosition;

    public SteeringDecisionContext2D(AIPerceptionManager2D memory,
        Vector2 agentPosition, Vector2 agentFacing, Vector2 agentVelocity, Vector2 nextPathPointDirection,
        Vector2 targetPosition)
    {
        this.Memory = memory;
        this.AgentPosition = agentPosition;
        this.AgentFacingDirection = agentFacing;
        this.AgentVelocity = agentVelocity;
        this.NextPathPointDirection = nextPathPointDirection;
        this.TargetPosition = targetPosition;
    }
}
