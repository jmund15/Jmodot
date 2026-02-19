namespace Jmodot.Examples.Movement.Strategies;

using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Movement.Strategies;

/// <summary>
/// Provides responsive "instant" feeling movement while still allowing external forces
/// (wave pull, knockback, etc.) to affect the character.
/// Uses high friction + high acceleration to achieve snappy control.
/// </summary>
[GlobalClass, Tool]
public partial class InstantMovementStrategy3D : BaseMovementStrategy3D
{
    [Export, RequiredExport] private Attribute _maxSpeedAttr = null!;

    /// <summary>
    /// How quickly the character stops when no input is given.
    /// Higher = snappier stop. Also decays external forces over time.
    /// </summary>
    [Export] public float Friction { get; set; } = 15.0f;

    /// <summary>
    /// How quickly the character reaches max speed.
    /// Higher = more instant response to input.
    /// </summary>
    [Export] public float Acceleration { get; set; } = 200.0f;

    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, IStatProvider stats, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(_maxSpeedAttr);

        // Work with horizontal velocity only (Y handled separately for gravity, etc.)
        var xzVel = new Vector3(currentVelocity.X, 0, currentVelocity.Z);

        // Apply friction - this preserves external forces but decays them over time
        xzVel -= xzVel * Friction * delta;

        // Apply player input as acceleration toward desired direction
        xzVel += desiredDirection * Acceleration * delta;

        // Clamp to max speed to prevent runaway velocity
        var xzSpeed = xzVel.Length();
        if (xzSpeed > maxSpeed)
        {
            xzVel = xzVel.Normalized() * maxSpeed;
        }

        return new Vector3(xzVel.X, currentVelocity.Y, xzVel.Z);
    }
}
