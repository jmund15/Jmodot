namespace Jmodot.Implementation.Interaction;

using System.Collections.Generic;
using Jmodot.Core.Interaction;

/// <summary>
/// Pure static scoring functions for grab target selection.
/// Computes a composite score: distance * directionFactor * typeFactor.
/// Lowest score wins. All factors contribute smoothly — no branching or priority gates.
///
/// Coordinate mapping: movement input Vector2(x,y) → world (X,Z).
/// Y axis is ignored (vertical position doesn't affect targeting).
/// </summary>
public static class GrabTargetScorer
{
    /// <summary>
    /// Computes a composite score for a single target. Lower = better.
    /// </summary>
    /// <param name="grabberPos">World position of the grabber.</param>
    /// <param name="targetPos">World position of the target.</param>
    /// <param name="facingDir">Player's facing direction as Vector2(x,z). Zero = no directional bias.</param>
    /// <param name="isReleasable">True for releasable grabbables (ingredients), false for non-releasable (potions).</param>
    /// <param name="config">Tuning parameters for direction and type influence.</param>
    /// <returns>Score where lower is better. Zero if grabber is on top of target.</returns>
    public static float ScoreTarget(
        Vector3 grabberPos, Vector3 targetPos,
        Vector2 facingDir, bool isReleasable,
        GrabTargetingConfig config)
    {
        var toTarget3D = targetPos - grabberPos;
        var toTarget2D = new Vector2(toTarget3D.X, toTarget3D.Z);
        float dist = toTarget2D.Length();

        if (dist < 0.001f) { return 0f; }

        // Direction factor: smoothly biases toward facing direction
        float dirMagnitude = Mathf.Min(facingDir.Length(), 1f);
        float dot = dirMagnitude > 0.001f
            ? (facingDir / facingDir.Length()).Dot(toTarget2D / dist)
            : 0f;
        float directionFactor = 1f - dot * config.DirectionInfluence * dirMagnitude;

        // Type factor: penalizes non-releasable grabbables (potions)
        float typeFactor = isReleasable ? 1f : config.NonReleasableTypePenalty;

        return dist * directionFactor * typeFactor;
    }

    /// <summary>
    /// Selects the best grab target from a list of candidates using composite scoring.
    /// </summary>
    /// <returns>The best target, or null if no candidates.</returns>
    public static IGrabbable3D? SelectBestTarget(
        Vector3 grabberPos, Vector2 facingDir,
        IReadOnlyList<IGrabbable3D> candidates,
        GrabTargetingConfig config)
    {
        if (candidates.Count == 0) { return null; }

        IGrabbable3D? best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (!candidate.IsGrabbable) { continue; }
            var node = (Node3D)candidate.GetUnderlyingNode();
            bool isReleasable = candidate is IThrowable3D or IDroppable3D;

            float score = ScoreTarget(grabberPos, node.GlobalPosition, facingDir, isReleasable, config);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }
}
