namespace Jmodot.Core.Movement.Quirks;

using Actors;

/// <summary>
/// The per-frame inputs a 3D movement quirk sees. Built once per tick by the quirk processor.
/// Dimension-parallel sibling: <see cref="MovementQuirkContext2D" />.
/// </summary>
public readonly struct MovementQuirkContext3D
{
    /// <summary>The impulse channel a quirk writes to. Quirks never set velocity directly.</summary>
    public readonly IMovementProcessor3D Movement;

    /// <summary>This frame's steering output — a unit vector, or zero when the agent has no heading.</summary>
    public readonly Vector3 DesiredDirection;

    public readonly Vector3 AgentVelocity;

    public MovementQuirkContext3D(IMovementProcessor3D movement, Vector3 desiredDirection, Vector3 agentVelocity)
    {
        Movement = movement;
        DesiredDirection = desiredDirection;
        AgentVelocity = agentVelocity;
    }
}
