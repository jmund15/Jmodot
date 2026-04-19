namespace Jmodot.Implementation.Movement.TurnRate;

using Jmodot.AI.Navigation;
using Jmodot.Core.Movement;
using Jmodot.Core.Stats;

/// <summary>
/// Constant turn rate limit regardless of speed. The entity turns at
/// _maxTurnRateDegrees per second at all velocities.
/// Dimension-parallel sibling: <see cref="UniformTurnRateProfile3D"/>.
/// </summary>
[GlobalClass, Tool]
public partial class UniformTurnRateProfile2D : TurnRateProfile2D
{
    [Export(PropertyHint.Range, "0, 720, 1")]
    private float _maxTurnRateDegrees = 180f;

    public override Vector2 Apply(
        Vector2 previousDirection, Vector2 desiredDirection,
        Vector2 currentVelocity, IStatProvider stats, float delta)
    {
        // Stationary entities can face any direction instantly
        if (currentVelocity.IsZeroApprox()) { return desiredDirection; }

        return AISteeringProcessor2D.ApplyTurnRateLimit(
            previousDirection, desiredDirection, _maxTurnRateDegrees, delta);
    }

    #region Test Helpers
#if TOOLS
    internal void SetMaxTurnRateDegreesForTest(float value) => _maxTurnRateDegrees = value;
#endif
    #endregion
}
