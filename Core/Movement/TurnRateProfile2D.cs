namespace Jmodot.Core.Movement;

using Stats;

/// <summary>
/// Abstract base for composable 2D turn rate profiles. Configured as an [Export] Resource
/// on BaseMovementStrategy2D. The MovementProcessor2D applies it as a preprocessing
/// step, clamping the desired direction before the strategy calculates velocity.
/// null profile = instant turning (no limit applied).
/// Dimension-parallel sibling: <see cref="TurnRateProfile3D"/>.
/// </summary>
[GlobalClass, Tool]
public abstract partial class TurnRateProfile2D : Resource
{
    /// <summary>
    /// Applies turn rate limiting to the desired direction.
    /// </summary>
    /// <param name="previousDirection">Movement direction from previous frame (normalized).</param>
    /// <param name="desiredDirection">Raw desired direction before limiting.</param>
    /// <param name="currentVelocity">Current velocity vector (for speed-based scaling).</param>
    /// <param name="stats">Stat provider for attribute lookups.</param>
    /// <param name="delta">Physics delta time.</param>
    /// <returns>The turn-rate-limited direction vector.</returns>
    public abstract Vector2 Apply(
        Vector2 previousDirection, Vector2 desiredDirection,
        Vector2 currentVelocity, IStatProvider stats, float delta);
}
