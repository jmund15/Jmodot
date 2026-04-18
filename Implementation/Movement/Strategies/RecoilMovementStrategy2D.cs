namespace Jmodot.Implementation.Movement.Strategies;

using Core.Stats;
using Registry;
using Shared;

/// <summary>
///     A recoil-oriented 2D strategy that decays momentum with configurable stopping friction,
///     while optionally allowing a small amount of steering influence from the desired direction.
/// </summary>
[GlobalClass]
public partial class RecoilMovementStrategy2D : BaseMovementStrategy2D
{
    [Export] public MovementStrategyStatOverride2D? StatOverride { get; set; }

    /// <summary>
    ///     Threshold below which recoil snaps to zero to prevent micro-sliding.
    /// </summary>
    /// <remarks>'0' means no snapping.</remarks>
    [Export] public float SnapVelocityToZeroThreshold { get; set; } = 0.01f;

    /// <summary>
    ///     Optional multiplier to make recoil friction stronger than normal locomotion friction.
    /// </summary>
    [Export] public float StopFrictionMultiplier { get; set; } = 2.0f;

    /// <summary>
    ///     How much desired input can steer the recoil vector.
    /// </summary>
    [Export] public float IntentEffect { get; set; } = 0f;

    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection, IStatProvider stats,
        MovementMode activeMode, float delta)
    {
        var friction = ResolveFriction(stats, activeMode);
        currentVelocity -= currentVelocity * (friction * StopFrictionMultiplier) * delta;

        if (SnapVelocityToZeroThreshold > 0f && currentVelocity.Length() <= SnapVelocityToZeroThreshold)
        {
            return Vector2.Zero;
        }

        if (IntentEffect <= 0f || desiredDirection.IsZeroApprox() || currentVelocity.IsZeroApprox())
        {
            return currentVelocity;
        }

        var currentMagnitude = currentVelocity.Length();
        var maxSteerPower = 0.5f;
        var nudge = desiredDirection.Normalized() * (IntentEffect * maxSteerPower);
        var newDirection = (currentVelocity.Normalized() + nudge).Normalized();
        return newDirection * currentMagnitude;
    }

    private float ResolveFriction(IStatProvider stats, MovementMode activeMode)
    {
        if (StatOverride != null)
        {
            return StatOverride.Friction;
        }

        if (GlobalRegistry.DB != null)
        {
            return stats.GetStatValue<float>(GlobalRegistry.DB.FrictionAttr, activeMode);
        }

        throw JmoLogger.LogAndRethrow(
            new System.InvalidOperationException(
                "RecoilMovementStrategy2D requires either a StatOverride or an initialized GlobalRegistry."),
            this);
    }
}
