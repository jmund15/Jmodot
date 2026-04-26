namespace Jmodot.Implementation.Movement.Strategies;

using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
/// 2D instant-response movement strategy where the target speed lerps between
/// <c>_minSpeedAttr</c> and <c>_maxSpeedAttr</c> based on the RAW intent magnitude. Designed for
/// analog-stick stealth / precision movement where firmer stick pushes map to faster movement
/// without any accel/decel ramp.
///
/// <b>Caller contract:</b> this strategy consumes magnitude from <c>desiredDirection</c>, so callers
/// MUST pass raw (non-normalized) intent. Passing a pre-normalized direction effectively hardcodes
/// speed to the lerp result at full magnitude. Direction is normalized inside the strategy for the
/// final velocity; magnitude is consumed for the speed lerp and then discarded.
///
/// Semantics per frame:
///   if desiredDirection == 0: return Vector2.Zero (no friction decay)
///   t = clamp(|desiredDirection| / MagnitudeThresholdForMaxSpeed, 0, 1)
///   targetSpeed = lerp(minSpeed, maxSpeed, t)
///   return desiredDirection.Normalized() * targetSpeed
///
/// <b>Zero-input behavior:</b> returns <see cref="Vector2.Zero"/>. No friction decay — use a
/// separate decay state (e.g. a Halt/Lag state with <see cref="ProportionalMovementStrategy2D"/>
/// or <see cref="IdleFrictionStrategy2D"/>) to handle the stop smoothly.
///
/// <b>Use cases:</b> analog-stick sneak / stealth movement, precision-movement modes, variable-speed
/// crouching, any state where input-stick magnitude should directly map to movement speed.
/// </summary>
[GlobalClass, Tool]
public partial class MagnitudeScaledInstantMovementStrategy2D : BaseMovementStrategy2D
{
    [ExportGroup("Stat Bindings")]
    [Export, RequiredExport] private Attribute _minSpeedAttr = null!;
    [Export, RequiredExport] private Attribute _maxSpeedAttr = null!;

    /// <summary>
    /// Raw intent magnitude at which the speed lerp reaches <c>_maxSpeedAttr</c>. Below this
    /// threshold, the lerp parameter scales linearly from 0 (any nonzero intent yields
    /// <c>_minSpeedAttr</c>) to 1 (<c>_maxSpeedAttr</c>).
    ///
    /// Default 0.3 matches typical analog-stick "half push" conventions — a player pushing the stick
    /// to ~30% deflection is already at full sneak speed, letting firmer pushes reserved for
    /// walk/run transitions triggered by the parent state machine.
    /// </summary>
    [ExportGroup("Instance Tuning")]
    [Export(PropertyHint.Range, "0.01,1.0,0.01")]
    public float MagnitudeThresholdForMaxSpeed { get; set; } = 0.3f;

    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        Vector2 previousDirection, IStatProvider stats, float delta)
    {
        if (desiredDirection.IsZeroApprox())
        {
            return Vector2.Zero;
        }

        var minSpeed = stats.GetStatValue<float>(_minSpeedAttr);
        var maxSpeed = stats.GetStatValue<float>(_maxSpeedAttr);

        var magnitude = desiredDirection.Length();
        var t = Mathf.Clamp(magnitude / MagnitudeThresholdForMaxSpeed, 0f, 1f);
        var targetSpeed = Mathf.Lerp(minSpeed, maxSpeed, t);

        return desiredDirection.Normalized() * targetSpeed;
    }

    #region Test Helpers
#if TOOLS
    internal void SetMinSpeedAttrForTest(Attribute attr) => _minSpeedAttr = attr;
    internal void SetMaxSpeedAttrForTest(Attribute attr) => _maxSpeedAttr = attr;
#endif
    #endregion
}
