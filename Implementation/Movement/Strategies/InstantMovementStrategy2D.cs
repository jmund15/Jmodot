namespace Jmodot.Implementation.Movement.Strategies;

using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
/// 2D instant-response movement at a single target speed. Direction is normalized internally;
/// raw intent magnitude is discarded. The simplest possible movement primitive — useful for
/// constant-speed walking, patrol locomotion, or any state where input direction drives movement
/// at a fixed speed without stick-magnitude modulation or accel/decel curves.
///
/// Semantics per frame:
///   if desiredDirection == 0: return Vector2.Zero (no friction decay)
///   return desiredDirection.Normalized() * stats.GetStatValue(_maxSpeedAttr)
///
/// <b>Zero-input behavior:</b> returns <see cref="Vector2.Zero"/>. Pair with a decay state (Halt/Lag
/// using <see cref="ProportionalMovementStrategy2D"/> or <see cref="IdleFrictionStrategy2D"/>) to
/// handle smooth stops.
///
/// <b>Contrast with <see cref="MagnitudeScaledInstantMovementStrategy2D"/>:</b> that strategy reads
/// stick magnitude and lerps between two speed attributes. This one discards magnitude and applies a
/// single speed. Pick this one for digital-like movement (fixed walk/patrol speed); pick the
/// magnitude-scaled variant for analog-stick stealth where stick push should modulate speed.
/// </summary>
[GlobalClass, Tool]
public partial class InstantMovementStrategy2D : BaseMovementStrategy2D
{
    [ExportGroup("Stat Bindings")]
    [Export, RequiredExport] private Attribute _maxSpeedAttr = null!;

    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        Vector2 previousDirection, IStatProvider stats, float delta)
    {
        if (desiredDirection.IsZeroApprox())
        {
            return Vector2.Zero;
        }

        var maxSpeed = stats.GetStatValue<float>(_maxSpeedAttr);
        return desiredDirection.Normalized() * maxSpeed;
    }

    #region Test Helpers
#if TOOLS
    internal void SetMaxSpeedAttrForTest(Attribute attr) => _maxSpeedAttr = attr;
#endif
    #endregion
}
