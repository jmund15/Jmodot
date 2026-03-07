namespace Jmodot.Implementation.Movement.TurnRate;

using Jmodot.AI.Navigation;
using Jmodot.Core.Movement;
using Jmodot.Core.Stats;

/// <summary>
/// Constant turn rate limit regardless of speed. The entity turns at
/// _maxTurnRateDegrees per second at all velocities.
/// Equivalent to the old baked-in InstantMovementStrategy3D turn rate.
/// </summary>
[GlobalClass, Tool]
public partial class UniformTurnRateProfile : TurnRateProfile
{
    [Export(PropertyHint.Range, "0, 720, 1")]
    private float _maxTurnRateDegrees = 180f;

    public override Vector3 Apply(
        Vector3 previousDirection, Vector3 desiredDirection,
        Vector3 currentVelocity, IStatProvider stats, float delta)
    {
        // Stationary entities can face any direction instantly
        var xzSpeed = new Vector3(currentVelocity.X, 0, currentVelocity.Z);
        if (xzSpeed.IsZeroApprox()) { return desiredDirection; }

        return AISteeringProcessor3D.ApplyTurnRateLimit(
            previousDirection, desiredDirection, _maxTurnRateDegrees, delta);
    }

    #region Test Helpers
#if TOOLS
    internal void SetMaxTurnRateDegreesForTest(float value) => _maxTurnRateDegrees = value;
#endif
    #endregion
}
