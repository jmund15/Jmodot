namespace Jmodot.Implementation.Movement.Strategies;

using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
/// 2D twin of <see cref="LinearMovementStrategy3D"/>. Linear movement using
/// MoveToward for constant-rate acceleration and friction. Speed is explicitly
/// capped by a maxSpeed stat.
/// </summary>
[GlobalClass, Tool]
public partial class LinearMovementStrategy2D : BaseMovementStrategy2D
{
    [ExportGroup("Stat Bindings")]
    [Export, RequiredExport] private Attribute _maxSpeedAttr = null!;
    [Export, RequiredExport] private Attribute _accelerationAttr = null!;
    [Export, RequiredExport] private Attribute _frictionAttr = null!;

    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        Vector2 previousDirection, IStatProvider stats, float delta)
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
            newVelocity = newVelocity.MoveToward(Vector2.Zero,
                stats.GetStatValue<float>(_frictionAttr) * delta);
        }

        return newVelocity;
    }

    #region Test Helpers
#if TOOLS
    internal void SetMaxSpeedAttrForTest(Attribute attr) => _maxSpeedAttr = attr;
    internal void SetAccelerationAttrForTest(Attribute attr) => _accelerationAttr = attr;
    internal void SetFrictionAttrForTest(Attribute attr) => _frictionAttr = attr;
#endif
    #endregion
}
