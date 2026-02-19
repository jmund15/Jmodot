namespace Jmodot.Implementation.Movement.Strategies;

using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
///     A concrete movement strategy that produces smooth, accelerated movement.
///     Uses MoveToward for both acceleration and friction, creating a robust default
///     for most ground or air characters.
/// </summary>
[GlobalClass, Tool]
public partial class AcceleratedMovementStrategy3D : BaseMovementStrategy3D
{
    [Export, RequiredExport] private Attribute _maxSpeedAttr = null!;
    [Export, RequiredExport] private Attribute _accelerationAttr = null!;
    [Export, RequiredExport] private Attribute _frictionAttr = null!;

    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(_maxSpeedAttr);
        var targetVelocity = desiredDirection * maxSpeed;
        var newVelocity = currentVelocity;

        if (!desiredDirection.IsZeroApprox())
        {
            newVelocity = newVelocity.MoveToward(targetVelocity,
                stats.GetStatValue<float>(_accelerationAttr) * delta);
        }
        else
        {
            newVelocity = newVelocity.MoveToward(Vector3.Zero,
                stats.GetStatValue<float>(_frictionAttr) * delta);
        }

        return newVelocity;
    }
}
