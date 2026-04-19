namespace Jmodot.Implementation.Movement.Strategies;

using Core.Movement;
using Core.Stats;
using Core.Movement.Strategies;

[GlobalClass, Tool]
public abstract partial class BaseMovementStrategy3D : Resource, IMovementStrategy3D
{
    /// <summary>
    /// Optional composable turn rate profile. When set, the MovementProcessor3D
    /// preprocesses the desired direction through this profile BEFORE calling
    /// CalculateVelocity. null = no turn rate limiting (instant turning).
    /// </summary>
    [ExportGroup("Turn Rate")]
    [Export] public TurnRateProfile3D? TurnProfile { get; set; }

    /// <summary>
    /// Override to return true if this strategy has internal turn logic that would
    /// conflict with an external TurnProfile. Used for misconfiguration warnings.
    /// </summary>
    public virtual bool HasInternalTurnLogic => false;

    public BaseMovementStrategy3D() { }
    public abstract Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection,
        Vector3 previousDirection, IStatProvider stats, float delta);
}
