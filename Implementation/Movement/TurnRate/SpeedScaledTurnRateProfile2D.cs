namespace Jmodot.Implementation.Movement.TurnRate;

using Jmodot.AI.Navigation;
using Jmodot.Core.Movement;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Attr = Jmodot.Core.Stats.Attribute;

/// <summary>
/// Turn rate that scales inversely with speed. At full speed, turning is limited
/// to _baseTurnRateDegrees/sec. At lower speeds, turning is proportionally faster.
/// Below _minSpeedRatio of max speed, turning is instant (no limit).
///
/// Formula: effectiveRate = _baseTurnRateDegrees / speedRatio
///   At max speed (ratio=1.0): 180°/sec
///   At half speed (ratio=0.5): 360°/sec
///   At 25% speed (ratio=0.25): 720°/sec
///   Below minSpeedRatio: instant turn
///
/// Dimension-parallel sibling: <see cref="SpeedScaledTurnRateProfile3D"/>.
/// </summary>
[GlobalClass, Tool]
public partial class SpeedScaledTurnRateProfile2D : TurnRateProfile2D
{
    [Export(PropertyHint.Range, "0, 720, 1")]
    private float _baseTurnRateDegrees = 180f;

    [Export, RequiredExport]
    private Attr _maxSpeedAttr = null!;

    /// <summary>
    /// Speed ratio below which turning is instant (unrestricted).
    /// 0.1 = below 10% of max speed, the entity can turn freely.
    /// </summary>
    [Export(PropertyHint.Range, "0, 1, 0.05")]
    private float _minSpeedRatio = 0.1f;

    public override Vector2 Apply(
        Vector2 previousDirection, Vector2 desiredDirection,
        Vector2 currentVelocity, IStatProvider stats, float delta)
    {
        return ApplySpeedScaled(
            previousDirection, desiredDirection, currentVelocity,
            stats, _maxSpeedAttr, _baseTurnRateDegrees, _minSpeedRatio, delta);
    }

    /// <summary>
    /// Pure static for testability. Computes speed-scaled turn rate limiting.
    /// </summary>
    public static Vector2 ApplySpeedScaled(
        Vector2 previousDirection, Vector2 desiredDirection, Vector2 currentVelocity,
        IStatProvider stats, Attr maxSpeedAttr,
        float baseTurnRateDegrees, float minSpeedRatio, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(maxSpeedAttr);
        if (maxSpeed <= 0f) { return desiredDirection; }

        var currentSpeed = currentVelocity.Length();
        var speedRatio = currentSpeed / maxSpeed;

        if (speedRatio < minSpeedRatio) { return desiredDirection; }

        var effectiveRate = baseTurnRateDegrees / speedRatio;

        return AISteeringProcessor2D.ApplyTurnRateLimit(
            previousDirection, desiredDirection, effectiveRate, delta);
    }

    #region Test Helpers
#if TOOLS
    internal void SetMaxTurnRateDegreesForTest(float value) => _baseTurnRateDegrees = value;
    internal void SetMaxSpeedAttrForTest(Attr attr) => _maxSpeedAttr = attr;
    internal void SetMinSpeedRatioForTest(float value) => _minSpeedRatio = value;
#endif
    #endregion
}
