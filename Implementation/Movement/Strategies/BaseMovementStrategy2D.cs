namespace Jmodot.Implementation.Movement.Strategies;

using Core.Stats;
using Core.Movement.Strategies;

[GlobalClass, Tool]
public abstract partial class BaseMovementStrategy2D : Resource, IMovementStrategy2D
{
    /// <summary>
    /// Override to return true if this strategy has internal turn logic that would
    /// conflict with an externally composed turn rate profile. Used for misconfiguration warnings
    /// once a TurnRateProfile2D abstraction is authored.
    /// </summary>
    // TODO(2d-parity): Expose `[Export] TurnRateProfile2D? TurnProfile` once the Vector2
    // turn-rate abstraction lands (brief §5 assumed it was shared with 3D; the 3D version
    // is Vector3-hardcoded). Strategies can still consume `previousDirection` directly for
    // internal turn logic in the meantime.
    public virtual bool HasInternalTurnLogic => false;

    public BaseMovementStrategy2D() { }
    public abstract Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        Vector2 previousDirection, IStatProvider stats, float delta);
}
