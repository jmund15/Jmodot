namespace Jmodot.Implementation.Movement.Strategies;

using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
/// 2D movement strategy using MoveToward toward a max-speed-scaled target with
/// asymmetric acceleration and deceleration stats. Feels snappier than
/// <see cref="LinearMovementStrategy2D"/> for character controllers — accel
/// determines start-up responsiveness, deceleration governs stop responsiveness
/// when input reverses or clears.
///
/// Stat bindings: max_speed, acceleration, deceleration, friction.
///   - acceleration: rate when input direction aligns with current velocity
///   - deceleration: rate when input opposes current velocity OR no input but velocity non-zero
///   - friction: explicit idle decay rate toward zero (when desiredDirection is zero AND
///     no deceleration would otherwise bring it to rest) — this is a secondary knob for
///     tuning stop feel independently of deceleration.
/// </summary>
[GlobalClass, Tool]
public partial class AcceleratedMovementStrategy2D : BaseMovementStrategy2D
{
    [ExportGroup("Stat Bindings")]
    [Export, RequiredExport] private Attribute _maxSpeedAttr = null!;
    [Export, RequiredExport] private Attribute _accelerationAttr = null!;
    [Export, RequiredExport] private Attribute _decelerationAttr = null!;
    [Export, RequiredExport] private Attribute _frictionAttr = null!;

    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        Vector2 previousDirection, IStatProvider stats, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(_maxSpeedAttr);
        var accel = stats.GetStatValue<float>(_accelerationAttr);
        var decel = stats.GetStatValue<float>(_decelerationAttr);
        var friction = stats.GetStatValue<float>(_frictionAttr);

        if (desiredDirection.IsZeroApprox())
        {
            // No input — pure friction decay toward zero.
            return currentVelocity.MoveToward(Vector2.Zero, friction * delta);
        }

        var targetVelocity = desiredDirection * maxSpeed;

        // Asymmetric rate: if current velocity's direction opposes desiredDirection
        // (dot product negative), use the deceleration rate for snappier turn-arounds.
        // Otherwise use acceleration. This produces responsive character control.
        var rate = (currentVelocity.Dot(desiredDirection) < 0f) ? decel : accel;

        return currentVelocity.MoveToward(targetVelocity, rate * delta);
    }

    #region Test Helpers
#if TOOLS
    internal void SetMaxSpeedAttrForTest(Attribute attr) => _maxSpeedAttr = attr;
    internal void SetAccelerationAttrForTest(Attribute attr) => _accelerationAttr = attr;
    internal void SetDecelerationAttrForTest(Attribute attr) => _decelerationAttr = attr;
    internal void SetFrictionAttrForTest(Attribute attr) => _frictionAttr = attr;
#endif
    #endregion
}
