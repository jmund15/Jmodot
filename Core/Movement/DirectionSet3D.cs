namespace Jmodot.Core.Movement;

using System.Collections.Generic;
using Implementation.Shared;

/// <summary>
///     A data-driven Resource that defines a discrete set of directional vectors.
///     This allows game-specific directional models (e.g., 4-way, 8-way) to be created
///     in the editor and used by systems to interpret a continuous direction vector.
/// </summary>
[GlobalClass]
public abstract partial class DirectionSet3D : Resource
{
    /// <summary>
    ///     Directions should always be normalized vectors.
    /// </summary>
    public IEnumerable<Vector3> Directions { get; protected set; } = null!;

    //TODO: see if needed for godot editor to allow export of resource
    //public DirectionSet3D() { }

    /// <summary>
    ///     A helper method to find the closest direction in this set to a given target direction.
    ///     This is the key to snapping a character's continuous input to a discrete animation state.
    /// </summary>
    /// <param name="targetDirection">The continuous, normalized direction to check against.</param>
    /// <returns>The index of the closest vector in the Directions array.</returns>
    //public int GetClosestDirectionIndex(Vector3 targetDirection)
    //{
    //    if (Directions == null || !Directions.Any())
    //    {
    //        GD.PrintErr($"DirectionSet3D '{ResourceName}' has no directions defined.");
    //        return -1; //TODO: throw exception instead? -1 should not be expected to be handled normally
    //    }
    //    if (Directions.Any(dir => dir.Length() == 0 || !dir.IsNormalized()))
    //    {
    //        GD.PrintErr($"DirectionSet3D '{ResourceName}' contains invalid directions. All directions must be normalized and non-zero.");
    //        return -1; //TODO: throw exception instead? -1 should not be expected to be handled normally
    //    }
    //    int bestIdx = 0;
    //    float maxDot = float.MinValue;
    //    var normalizedTarget = targetDirection.Normalized();

    //    for (int i = 0; i < Directions.Count; i++)
    //    {
    //        // The dot product of two normalized vectors gives the cosine of the angle between them.
    //        // A higher dot product means a smaller angle.
    //        float dot = Directions[i].Dot(normalizedTarget);
    //        if (dot > maxDot)
    //        {
    //            maxDot = dot;
    //            bestIdx = i;
    //        }
    //    }
    //    return bestIdx;
    //}
    public Vector3 GetClosestDirection(Vector3 targetDirection)
    {
        //int index = GetClosestDirectionIndex(targetDirection);
        //if (index >= 0 && index < Directions.Count)
        //{
        //    return Directions[index];
        //}
        Vector3? closestDir = null;
        var maxDot = float.MinValue;
        var normalizedTarget = targetDirection.Normalized();
        foreach (var dir in this.Directions)
        {
            // The dot product of two normalized vectors gives the cosine of the angle between them.
            // A higher dot product means a smaller angle.
            var dot = dir.Dot(normalizedTarget);
            if (dot > maxDot)
            {
                maxDot = dot;
                closestDir = dir;
            }
        }

        if (closestDir == null)
            JmoLogger.Error(this,
                $"No valid direction found for {targetDirection} within the DirectionSet3D '{this.ResourceName}'.");
        // TODO: print out all directions in direction set for error case
        return closestDir ?? Vector3.Zero;
    }
}
// In "Jmo/Core/State/DirectionSet.cs"

//namespace Jmo.Core.State
//{
//    /// <summary>
//    /// A data-driven Resource that defines a discrete set of directional vectors.
//    /// This allows game-specific directional models (e.g., 4-way, 8-way) to be created
//    /// in the editor and used by systems to interpret a continuous direction vector.
//    /// </summary>
//    [GlobalClass]
//    public partial class DirectionSet : Resource
//    {
//        /// <summary>The collection of normalized direction vectors that make up this set.</summary>
//        [Export] public Array<Vector3> Directions { get; private set; } = new();

//        public int GetClosestDirectionIndex(Vector3 targetDirection)
//        {
//            if (Directions == null || Directions.Count == 0) return -1;

//            int bestIndex = 0;
//            float maxDot = -2.0f; // Start with a value lower than any possible dot product

//            for (int i = 0; i < Directions.Count; i++)
//            {
//                // The dot product of two normalized vectors gives the cosine of the angle between them.
//                // A higher dot product means a smaller angle.
//                float dot = Directions[i].Dot(targetDirection);
//                if (dot > maxDot)
//                {
//                    maxDot = dot;
//                    bestIndex = i;
//                }
//            }
//            return bestIndex;
//        }
//    }
//}
