namespace Jmodot.Implementation.Movement.Strategies;

using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
/// 2D movement strategy for idle/halt states: ignores desiredDirection entirely
/// and decays currentVelocity toward zero using a configurable friction stat.
/// Useful for HaltState / IdleState / LagState where input is suppressed but
/// existing momentum should taper to rest.
///
/// Below a configurable stop threshold, velocity snaps to exactly zero to
/// avoid subpixel jitter.
/// </summary>
[GlobalClass, Tool]
public partial class IdleFrictionStrategy2D : BaseMovementStrategy2D
{
    [ExportGroup("Stat Bindings")]
    [Export, RequiredExport] private Attribute _frictionAttr = null!;

    [ExportGroup("Behavior")]
    /// <summary>
    /// Speed below which velocity is snapped to zero. Prevents sub-pixel
    /// drift and unintended perpetual motion from floating-point residue.
    /// </summary>
    [Export(PropertyHint.Range, "0, 10, 0.01")]
    private float _stopThreshold = 0.1f;

    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        Vector2 previousDirection, IStatProvider stats, float delta)
    {
        var friction = stats.GetStatValue<float>(_frictionAttr);
        var next = currentVelocity.MoveToward(Vector2.Zero, friction * delta);

        if (next.LengthSquared() < _stopThreshold * _stopThreshold)
        {
            return Vector2.Zero;
        }

        return next;
    }

    #region Test Helpers
#if TOOLS
    internal void SetFrictionAttrForTest(Attribute attr) => _frictionAttr = attr;
    internal void SetStopThresholdForTest(float threshold) => _stopThreshold = threshold;
#endif
    #endregion
}
