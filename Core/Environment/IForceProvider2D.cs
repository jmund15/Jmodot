namespace Jmodot.Core.Environment;

/// <summary>
///     A universal interface for any environmental object or area that can exert a
///     physical force on an actor. This allows for the creation of wind zones,
///     conveyor belts, rivers, or tractor beams in a modular and standardized way.
/// </summary>
public interface IForceProvider2D
{
    /// <summary>
    ///     Calculates the force vector this provider should apply to a specific target.
    /// </summary>
    /// <param name="target">The actor (e.g., CharacterBody2D) being affected.</param>
    /// <returns>A Vector2 representing the velocity to be added for this frame.</returns>
    Vector2 GetForceFor(Node2D target);
}
