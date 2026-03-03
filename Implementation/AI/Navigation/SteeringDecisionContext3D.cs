namespace Jmodot.Implementation.AI.Navigation;

using Perception;

/// <summary>
///     An immutable, high-performance struct that provides a snapshot of all necessary world
///     and agent state for a single decision-making frame. It is created by the AIAgent
///     and passed to the AISteeringProcessor to ensure all considerations operate on the
///     same consistent data set.
/// </summary>
public readonly struct SteeringDecisionContext3D
{
    /// <summary>
    /// Spatial perception (threats/items/allies). Populated by AIPerceptionManager3D.
    /// null for critters that don't use perception memory (e.g., pure wander or flee-only agents).
    /// Read by StaticBody3DConsideration and VelocityBody3DConsideration.
    /// </summary>
    public readonly AIPerceptionManager3D Memory;

    /// <summary>
    /// Current global position of the agent. Always populated.
    /// </summary>
    public readonly Vector3 AgentPosition;

    /// <summary>
    /// Normalized facing direction (-GlobalTransform.Basis.Z). Always populated.
    /// </summary>
    public readonly Vector3 AgentFacingDirection;

    /// <summary>
    /// Current velocity vector of the agent. Always populated.
    /// Read by VelocityBody3DConsideration for relative velocity calculations.
    /// </summary>
    public readonly Vector3 AgentVelocity;

    /// <summary>
    /// Normalized direction to next navigation path waypoint. Zero when no navigation
    /// path is active (e.g., pure wander/flee states). Read exclusively by
    /// NavigationPath3DConsideration.
    /// </summary>
    public readonly Vector3 NextPathPointDirection;

    /// <summary>
    /// Current navigation target position (AINavigator3D.TargetPosition). Zero when no
    /// nav target is set. Used by DistanceScalingModifier3D — modifiers dependent on
    /// this field should document the dependency.
    /// </summary>
    public readonly Vector3 TargetPosition;

    /// <summary>
    /// Physics frame delta time (seconds). Used by AISteeringProcessor3D for turn rate
    /// smoothing. Zero or unset disables time-dependent smoothing calculations.
    /// </summary>
    public readonly float PhysicsDelta;

    public SteeringDecisionContext3D(AIPerceptionManager3D memory,
        Vector3 agentPosition, Vector3 agentFacing, Vector3 agentVelocity, Vector3 nextPathPointDirection,
        Vector3 targetPosition, float physicsDelta = 0f)
    {
        this.Memory = memory;
        this.AgentPosition = agentPosition;
        this.AgentFacingDirection = agentFacing;
        this.AgentVelocity = agentVelocity;
        this.NextPathPointDirection = nextPathPointDirection;
        this.TargetPosition = targetPosition;
        this.PhysicsDelta = physicsDelta;
    }
}
