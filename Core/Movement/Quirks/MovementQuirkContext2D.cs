namespace Jmodot.Core.Movement.Quirks;

using Actors;

/// <summary>
/// The per-frame inputs a 2D movement quirk sees. Built once per tick by the quirk processor.
/// Dimension-parallel sibling: <see cref="MovementQuirkContext3D" />.
/// </summary>
public readonly struct MovementQuirkContext2D
{
    /// <summary>The impulse channel a quirk writes to. Quirks never set velocity directly.</summary>
    public readonly IImpulseReceiver2D Movement;

    /// <summary>This frame's steering output — a unit vector, or zero when the agent has no heading.</summary>
    public readonly Vector2 DesiredDirection;

    public readonly Vector2 AgentVelocity;

    public MovementQuirkContext2D(IImpulseReceiver2D movement, Vector2 desiredDirection, Vector2 agentVelocity)
    {
        Movement = movement;
        DesiredDirection = desiredDirection;
        AgentVelocity = agentVelocity;
    }
}
