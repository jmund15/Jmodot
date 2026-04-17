namespace Jmodot.Core.Movement.Strategies;

using Stats;

public interface IMovementStrategy2D
{
    /// <summary>
    ///     Calculates the character-driven portion of a 2D velocity based on a specific movement context.
    /// </summary>
    /// <param name="currentVelocity">The controller's current velocity.</param>
    /// <param name="desiredDirection">The desired 2D movement direction. Magnitude may be used by the strategy.</param>
    /// <param name="stats">The stat provider to query for movement properties.</param>
    /// <param name="activeMode">The definitive movement context. This is NOT optional.</param>
    /// <param name="delta">The physics frame delta time.</param>
    /// <returns>The new velocity vector reflecting character-driven movement.</returns>
    Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection, IStatProvider stats,
        MovementMode activeMode, float delta);
}
