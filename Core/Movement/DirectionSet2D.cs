namespace Jmodot.Core.Movement;

using System.Collections.Generic;
using Implementation.Shared;

/// <summary>
///     A data-driven Resource that defines a discrete set of 2D directional vectors.
///     This allows game-specific directional models such as 4-way or 8-way movement
///     to be authored once and reused for animation, aiming, or snapped locomotion.
/// </summary>
[GlobalClass]
public abstract partial class DirectionSet2D : Resource
{
    public IEnumerable<Vector2> Directions { get; protected set; } = null!;

    public Vector2 GetClosestDirection(Vector2 targetDirection)
    {
        Vector2? closestDir = null;
        var maxDot = float.MinValue;
        var normalizedTarget = targetDirection.Normalized();

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
                $"No valid direction found for {targetDirection} within the DirectionSet2D '{this.ResourceName}'.");
        }

        return closestDir ?? Vector2.Zero;
    }
}
