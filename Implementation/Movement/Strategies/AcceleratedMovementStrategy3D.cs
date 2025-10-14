namespace Jmodot.Implementation.Movement.Strategies;

using Core.Movement.Strategies;
using Core.Stats;
using PushinPotions.Global;
using Registry;

/// <summary>
///     A concrete movement strategy that produces smooth, accelerated movement based on the
///     standard properties of a VelocityProfile. This is a robust default for most ground or air characters.
/// </summary>
[GlobalClass]
public partial class AcceleratedMovementStrategy3D : Resource, IMovementStrategy3D
{
    public Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats, float delta)
    {
        var registry = GlobalRegistry.Instance;
        // The strategy's recipe for calculating the new velocity.
        var targetVelocity = desiredDirection * stats.GetStatValue<float>(GlobalRegistry.DB.MaxSpeedAttr);
        var newVelocity = currentVelocity;

        // If there's input, accelerate towards the target.
        if (!desiredDirection.IsZeroApprox())
        {
            newVelocity = newVelocity.MoveToward(targetVelocity,
                stats.GetStatValue<float>(GlobalRegistry.DB.AccelerationAttr) * delta);
        }
        else // If no input, apply friction.
        {
            // Note: a more complex strategy could use the BrakingMultiplier here.
            newVelocity = newVelocity.MoveToward(Vector3.Zero,
                stats.GetStatValue<float>(GlobalRegistry.DB.FrictionAttr) * delta);
        }

        // TODO: make sure y is handled correctly (does acceleration/friction apply to gravity?)
        return newVelocity;
    }
}
