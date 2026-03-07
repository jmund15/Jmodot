namespace Jmodot.Implementation.Movement.Strategies;

using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
///     Linear movement strategy using MoveToward for constant-rate acceleration and friction.
///     Speed is explicitly capped by a maxSpeed stat — contrasts with TerminalVelocityMovementStrategy3D
///     where max speed emerges from the acceleration/friction ratio.
/// </summary>
[GlobalClass, Tool]
public partial class LinearMovementStrategy3D : BaseMovementStrategy3D
{
    [ExportGroup("Stat Bindings")]
    [Export, RequiredExport] private Attribute _maxSpeedAttr = null!;
    [Export, RequiredExport] private Attribute _accelerationAttr = null!;
    [Export, RequiredExport] private Attribute _frictionAttr = null!;

    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, Vector3 previousDirection, IStatProvider stats, float delta)
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
