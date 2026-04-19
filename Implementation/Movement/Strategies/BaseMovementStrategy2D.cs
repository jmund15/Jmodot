namespace Jmodot.Implementation.Movement.Strategies;

using Core.Movement;
using Core.Stats;
using Core.Movement.Strategies;

[GlobalClass, Tool]
public abstract partial class BaseMovementStrategy2D : Resource, IMovementStrategy2D
{
    /// <summary>
    /// Optional composable turn rate profile. When set, the MovementProcessor2D
    /// preprocesses the desired direction through this profile BEFORE calling
    /// CalculateVelocity. null = no turn rate limiting (instant turning).
    /// </summary>
    [ExportGroup("Turn Rate")]
    [Export] public TurnRateProfile2D? TurnProfile { get; set; }

    /// <summary>
    /// Override to return true if this strategy has internal turn logic that would
    /// conflict with an external TurnProfile. Used for misconfiguration warnings.
    /// </summary>
    public virtual bool HasInternalTurnLogic => false;

    public BaseMovementStrategy2D() { }
    public abstract Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        Vector2 previousDirection, IStatProvider stats, float delta);
}
