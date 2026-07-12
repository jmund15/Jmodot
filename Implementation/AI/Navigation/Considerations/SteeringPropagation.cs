namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System;
using System.Collections.Generic;
using Godot;
using Core.Movement;

/// <summary>
/// Static utility for bleeding steering scores to neighboring directions.
/// Used by context-based steering to smooth out sharp score discontinuities,
/// making AI movement feel more natural by propagating influence to nearby directions.
/// </summary>
public static class SteeringPropagation
{
    /// <summary>
    /// Propagates non-zero scores from each direction to its neighbors with diminishing weight.
    /// Bleed is symmetric in sign — positives spread interest, negatives spread danger gradient —
    /// so channel routing downstream stays consistent. Scores accumulate additively: a direction
    /// that receives bleed from multiple sources gets the sum of all contributions.
    /// </summary>
    /// <param name="scores">Direction → score map. Modified in-place.</param>
    /// <param name="directions">Ordered list of directions (circular — last wraps to first).</param>
    /// <param name="neighborCount">How many neighbors on each side receive bleed.</param>
    /// <param name="diminishWeight">Multiplier per step (0 = no spread, 1 = no falloff).</param>
    public static void PropagateScores(
        Dictionary<Vector3, float> scores,
        List<Vector3> directions,
        int neighborCount,
        float diminishWeight)
    {
        PropagateScoresCore(scores, directions, neighborCount, diminishWeight);
    }

    /// <summary>
    /// Propagates scores along a DirectionSet3D's ordered circular ring
    /// (<see cref="DirectionSet3D.OrderedDirections"/>). No-ops when the set is null or
    /// <see cref="DirectionSet3D.HasCircularOrder"/> is false — non-planar / sub-three sets have no
    /// well-defined neighbor topology, so bleeding along an arbitrary order would corrupt scores.
    /// </summary>
    public static void PropagateScores(
        Dictionary<Vector3, float> scores,
        DirectionSet3D directions,
        int neighborCount,
        float diminishWeight)
    {
        if (directions == null || !directions.HasCircularOrder)
        {
            return;
        }

        PropagateScoresCore(scores, directions.OrderedDirections, neighborCount, diminishWeight);
    }

    private static void PropagateScoresCore(
        Dictionary<Vector3, float> scores,
        IReadOnlyList<Vector3> directions,
        int neighborCount,
        float diminishWeight)
    {
        if (directions == null || directions.Count == 0)
        {
            return;
        }

        int count = directions.Count;

        // Snapshot original scores so propagation reads from the pre-bleed state
        var original = new float[count];
        for (int i = 0; i < count; i++)
        {
            original[i] = scores.GetValueOrDefault(directions[i], 0f);
        }

        // For each direction with a non-zero score, bleed to neighbors
        for (int i = 0; i < count; i++)
        {
            if (original[i] == 0f)
            {
                continue;
            }

            for (int step = 1; step <= neighborCount; step++)
            {
                float bleed = original[i] * MathF.Pow(diminishWeight, step);

                // Bleed to the right neighbor (wrap around)
                int rightIdx = (i + step) % count;
                scores[directions[rightIdx]] += bleed;

                // Bleed to the left neighbor (wrap around)
                int leftIdx = ((i - step) % count + count) % count;
                scores[directions[leftIdx]] += bleed;
            }
        }
    }
}
