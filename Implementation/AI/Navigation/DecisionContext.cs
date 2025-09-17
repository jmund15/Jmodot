#region

using Jmodot.Implementation.AI.Perception;

#endregion

namespace Jmodot.Implementation.AI.Navigation;

/// <summary>
///     An immutable, high-performance struct that provides a snapshot of all necessary world
///     and agent state for a single decision-making frame. It is created by the AIAgent
///     and passed to the AISteeringProcessor to ensure all considerations operate on the
///     same consistent data set.
/// </summary>
public readonly struct DecisionContext
{
    public readonly AIPerceptionManager Memory;
    public readonly Vector3 AgentPosition;
    public readonly Vector3 AgentFacingDirection;
    public readonly Vector3 AgentVelocity;
    public readonly Vector3 NextPathPointDirection;
    public readonly Vector3 TargetPosition;

    public DecisionContext(AIPerceptionManager memory,
        Vector3 agentPosition, Vector3 agentFacing, Vector3 agentVelocity, Vector3 nextPathPointDirection,
        Vector3 targetPosition)
    {
        Memory = memory;
        AgentPosition = agentPosition;
        AgentFacingDirection = agentFacing;
        AgentVelocity = agentVelocity;
        NextPathPointDirection = nextPathPointDirection;
        TargetPosition = targetPosition;
    }
}