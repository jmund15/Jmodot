namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Core.Movement;
using Godot;

/// <summary>
/// The one owner of "score each discrete direction by its positive alignment with a desired
/// vector". Every consideration that steers toward or away from one direction calls this instead
/// of re-deriving the flatten/normalize/positive-dot loop.
/// </summary>
public static class DirectionScoring
{
    private const float Epsilon = 0.001f;

    /// <summary>
    /// Scores each direction in <paramref name="directions"/> by its positive alignment with
    /// <paramref name="response"/>; non-positive bins keep their existing value (a fresh map stays
    /// zero there). <paramref name="scale"/> multiplies each positive alignment once.
    /// <paramref name="into"/> receives the scores and is returned; it must be a map this scoring
    /// pass owns and has not written yet. <paramref name="raw"/> skips the three transforms the
    /// flat branch performs — the XZ flatten, the normalize and the epsilon skip — leaving the
    /// plain 3D dot, which is what an unflattened path-direction caller needs.
    /// </summary>
    public static Dictionary<Vector3, float> ByAlignment(DirectionSet3D directions, Vector3 response,
        float scale = 1f, Dictionary<Vector3, float>? into = null, bool raw = false)
    {
        var scores = into ?? directions.Directions.ToDictionary(dir => dir, _ => 0f);

        foreach (var dir in directions.Directions)
        {
            var flatDir = dir;
            if (!raw)
            {
                flatDir.Y = 0f;
                if (flatDir.LengthSquared() < Epsilon) { continue; }
                flatDir = flatDir.Normalized();
            }

            float alignment = flatDir.Dot(response);
            if (alignment > 0f) { scores[dir] = alignment * scale; }
        }

        return scores;
    }
}
