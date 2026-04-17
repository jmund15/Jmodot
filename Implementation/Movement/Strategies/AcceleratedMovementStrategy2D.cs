namespace Jmodot.Implementation.Movement.Strategies;

using Core.Movement.Strategies;
using Core.Stats;
using Registry;
using Shared;
using JmodotAttribute = Jmodot.Core.Stats.Attribute;

/// <summary>
///     A concrete 2D movement strategy that applies acceleration while input is present and
///     multiplicative friction when input is released. This matches the feel of many top-down
///     characters whose movement needs a bit of carried momentum without requiring a physics body.
/// </summary>
[GlobalClass]
public partial class AcceleratedMovementStrategy2D : Resource, IMovementStrategy2D
{
    public JmodotAttribute? MaxSpeedAttribute { get; set; }
    public JmodotAttribute? AccelerationAttribute { get; set; }
    public JmodotAttribute? FrictionAttribute { get; set; }

    public Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection, IStatProvider stats,
        MovementMode activeMode, float delta)
    {
        var maxSpeedAttr = ResolveAttribute(MaxSpeedAttribute, db => db.MaxSpeedAttr, nameof(MaxSpeedAttribute));
        var accelerationAttr = ResolveAttribute(AccelerationAttribute, db => db.AccelerationAttr,
            nameof(AccelerationAttribute));
        var frictionAttr = ResolveAttribute(FrictionAttribute, db => db.FrictionAttr, nameof(FrictionAttribute));

        var maxSpeed = stats.GetStatValue<float>(maxSpeedAttr, activeMode);
        var acceleration = stats.GetStatValue<float>(accelerationAttr, activeMode);
        var friction = stats.GetStatValue<float>(frictionAttr, activeMode);

        var newVelocity = currentVelocity;

        if (!desiredDirection.IsZeroApprox())
        {
            newVelocity += desiredDirection * acceleration * delta;
            if (newVelocity.Length() > maxSpeed)
            {
                newVelocity = newVelocity.LimitLength(maxSpeed);
            }
        }
        else if (!newVelocity.IsZeroApprox())
        {
            newVelocity -= newVelocity * friction * delta;
            if (newVelocity.LengthSquared() <= 0.001f)
            {
                newVelocity = Vector2.Zero;
            }
        }

        return newVelocity;
    }

    private JmodotAttribute ResolveAttribute(JmodotAttribute? configuredAttribute, System.Func<GameRegistry, JmodotAttribute> registrySelector,
        string attributeName)
    {
        if (configuredAttribute != null)
        {
            return configuredAttribute;
        }

        if (GlobalRegistry.DB != null)
        {
            return registrySelector(GlobalRegistry.DB);
        }

        throw JmoLogger.LogAndRethrow(
            new System.InvalidOperationException(
                $"AcceleratedMovementStrategy2D requires '{attributeName}' or an initialized GlobalRegistry."),
            this);
    }
}
