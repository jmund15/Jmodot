namespace Jmodot.Core.Movement.Strategies;

using Stats;

public interface IMovementStrategy2D
{
    /// <summary>
    ///     Calculates the character-driven portion of velocity based on a specific movement context.
    /// </summary>
    /// <param name="currentVelocity">The controller's current velocity.</param>
    /// <param name="desiredDirection">The normalized direction the character wants to move in.</param>
    /// <param name="stats">The stat provider to query for physics properties.</param>
    /// <param name="activeContext">The movement context.</param>
    /// <param name="delta">The physics frame delta time.</param>
    /// <returns>The new velocity vector reflecting character-driven movement.</returns>
    Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection, IStatProvider stats, float delta);
}
