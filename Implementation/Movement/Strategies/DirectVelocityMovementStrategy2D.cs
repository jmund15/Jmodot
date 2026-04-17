namespace Jmodot.Implementation.Movement.Strategies;

using Core.Movement.Strategies;
using Core.Stats;
using Registry;
using Shared;
using JmodotAttribute = Jmodot.Core.Stats.Attribute;

/// <summary>
///     A direct 2D movement strategy that maps input immediately to a target velocity. This is
///     useful for locomotion modes such as walk, strafe, or authored sneak movement where the
///     calling system decides the desired speed curve and the strategy simply drives the controller.
/// </summary>
[GlobalClass]
public partial class DirectVelocityMovementStrategy2D : Resource, IMovementStrategy2D
{
    public JmodotAttribute? MaxSpeedAttribute { get; set; }

    public Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection, IStatProvider stats,
        MovementMode activeMode, float delta)
    {
        var maxSpeedAttr = ResolveAttribute(MaxSpeedAttribute);
        var maxSpeed = stats.GetStatValue<float>(maxSpeedAttr, activeMode);
        return desiredDirection * maxSpeed;
    }

    private JmodotAttribute ResolveAttribute(JmodotAttribute? configuredAttribute)
    {
        if (configuredAttribute != null)
        {
            return configuredAttribute;
        }

        if (GlobalRegistry.DB != null)
        {
            return GlobalRegistry.DB.MaxSpeedAttr;
        }

        throw JmoLogger.LogAndRethrow(
            new System.InvalidOperationException(
                "DirectVelocityMovementStrategy2D requires 'MaxSpeedAttribute' or an initialized GlobalRegistry."),
            this);
    }
}
