namespace Jmodot.Implementation.Movement.Strategies;

using Core.Stats;
using Registry;
using Shared;

/// <summary>
///     A friction-only 2D strategy that decays any existing velocity without applying new thrust.
///     It is useful for idle, recovery, or defend states that should preserve momentum briefly
///     while still settling cleanly to zero.
/// </summary>
[GlobalClass]
public partial class IdleFrictionStrategy2D : BaseMovementStrategy2D
{
    [Export] public MovementStrategyStatOverride2D? StatOverride { get; set; }

    /// <summary>
    ///     Threshold below which velocity snaps to zero to prevent micro-sliding.
    /// </summary>
    /// <remarks>'0' means no snapping.</remarks>
    [Export] public float SnapVelocityToZeroThreshold { get; set; } = 0.1f;

    /// <summary>
    ///     Multiplier for friction when idle (higher = faster stop).
    /// </summary>
    [Export] public float FrictionMultiplier { get; set; } = 1.0f;

    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection, IStatProvider stats,
        MovementMode activeMode, float delta)
    {
        var friction = ResolveFriction(stats, activeMode);
        var newVelocity = currentVelocity - (currentVelocity * (friction * FrictionMultiplier) * delta);

        if (SnapVelocityToZeroThreshold > 0f &&
            newVelocity.LengthSquared() <= SnapVelocityToZeroThreshold * SnapVelocityToZeroThreshold)
        {
            return Vector2.Zero;
        }

        return newVelocity;
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
                "IdleFrictionStrategy2D requires either a StatOverride or an initialized GlobalRegistry."),
            this);
    }
}
