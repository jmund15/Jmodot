namespace Jmodot.Core.Movement;

using System.Collections.Generic;
using Implementation.Shared;

/// <summary>
///     A data-driven Resource that defines a discrete set of directional vectors.
///     This allows game-specific directional models (e.g., 4-way, 8-way) to be created
///     in the editor and used by systems to interpret a continuous direction vector.
/// </summary>
[GlobalClass, Tool]
public abstract partial class DirectionSet3D : Resource
{
    /// <summary>
    /// The collection of normalized direction vectors that make up this set.
    /// This property is populated by the concrete implementations.
    /// </summary>
    public IEnumerable<Vector3> Directions { get; protected set; } = null!;

    /// <summary>
    /// Finds the closest direction vector in this set to a given target direction.
    /// This is the core logic that snaps a continuous input (like from a joystick)
    /// to a discrete direction.
    /// </summary>
    /// <param name="targetDirection">The continuous, normalized direction to check against.</param>
    /// <returns>The closest matching Vector3 from the Directions collection, or Vector3.Zero if none found.</returns>
    public Vector3 GetClosestDirection(Vector3 targetDirection)
    {
        if (targetDirection.LengthSquared() < 1e-6f)
        {
            return Vector3.Zero;
        }

        Vector3? closestDir = null;
        var maxDot = float.MinValue;
        var normalizedTarget = targetDirection.Normalized();

        // The dot product of two normalized vectors gives the cosine of the angle between them.
        // A higher dot product means a smaller angle, so we are looking for the maximum dot product.
        foreach (var dir in this.Directions)
        {
            var dot = dir.Dot(normalizedTarget);
            if (dot > maxDot)
            {
                maxDot = dot;
                closestDir = dir;
            }
        }

        if (closestDir == null)
        {
            JmoLogger.Error(this,
                $"No valid direction found for {targetDirection} within the DirectionSet3D '{this.ResourceName}'.");
        }

        return closestDir ?? Vector3.Zero;
    }
}
