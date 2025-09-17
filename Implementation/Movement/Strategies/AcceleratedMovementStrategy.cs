namespace Jmodot.Implementation.Movement.Strategies;

using Core.Movement.Strategies;
using Core.Stats;
using Registry;

/// <summary>
///     A concrete movement strategy that produces smooth, accelerated movement based on the
///     standard properties of a VelocityProfile. This is a robust default for most ground or air characters.
/// </summary>
[GlobalClass]
public partial class AcceleratedMovementStrategy : Resource, IMovementStrategy
{
    public Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats,
        MovementMode activeMode, float delta)
    {
        var registry = GlobalRegistry.Instance;
        // The strategy's recipe for calculating the new velocity.
        var targetVelocity = desiredDirection * stats.GetStatValue<float>(GlobalRegistry.DB.MaxSpeedAttr, activeMode);
        var newVelocity = currentVelocity;

        // If there's input, accelerate towards the target.
        if (!desiredDirection.IsZeroApprox())
        {
            newVelocity = newVelocity.MoveToward(targetVelocity,
                stats.GetStatValue<float>(GlobalRegistry.DB.AccelerationAttr, activeMode) * delta);
        }
        else // If no input, apply friction.
        {
            // Note: a more complex strategy could use the BrakingMultiplier here.
            newVelocity = newVelocity.MoveToward(Vector3.Zero,
                stats.GetStatValue<float>(GlobalRegistry.DB.FrictionAttr, activeMode) * delta);
        }

        // TODO: make sure y is handled correctly (does acceleration/friction apply to gravity?)
        return newVelocity;
    }
}
