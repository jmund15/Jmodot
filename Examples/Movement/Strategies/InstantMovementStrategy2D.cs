namespace Jmodot.Examples.Movement.Strategies;

using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Movement.Strategies;

/// <summary>
/// Truly instant movement: the character moves at exactly desiredDirection * maxSpeed
/// every frame. No friction, no acceleration ramp-up, no momentum.
/// Stopping is instant. Direction changes are instant.
/// Turn rate is NOT handled here — strategies that want turn rate limiting should
/// override HasInternalTurnLogic and consume previousDirection directly.
/// </summary>
[GlobalClass, Tool]
public partial class InstantMovementStrategy2D : BaseMovementStrategy2D
{
    [Export, RequiredExport] private Attribute _maxSpeedAttr = null!;

    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        Vector2 previousDirection, IStatProvider stats, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(_maxSpeedAttr);
        return desiredDirection * maxSpeed;
    }
}
