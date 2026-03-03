namespace Jmodot.Core.Physics;

/// <summary>
/// Implemented by physics objects that need ground-plane awareness.
/// Returns the distance from the object's origin to the bottom of its
/// body collision shape, accounting for shape type, local transform,
/// and scale. Spawners use this to position objects so their collision
/// bottom sits at the target Y coordinate.
/// </summary>
public interface IGroundable
{
    /// <summary>
    /// Distance from this object's origin to the bottom of its body collision extent.
    /// Positive value = bottom is below origin (typical for center-origin objects).
    /// Zero = bottom is at or above origin (no correction needed).
    /// Spawners apply: <c>obj.GlobalPosition += Vector3.Up * obj.GroundOffset;</c>
    /// </summary>
    float GroundOffset { get; }
}
