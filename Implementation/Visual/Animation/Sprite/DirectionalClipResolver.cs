namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System;
using System.Collections.Generic;
using Core.Visual.Animation.Sprite;
using Godot;

/// <summary>
/// Pure resolution of which concrete clip an animator should play for a directional request,
/// degrading through up to three tiers per the caller's <see cref="SlotFallbackPolicy"/>.
/// </summary>
public static class DirectionalClipResolver
{
    /// <summary>
    /// Resolves the clip name to play, or null when nothing resolves.
    /// Tier 1 (both policies): the exact "{baseName}{separator}{directionLabel}".
    /// Tier 2 (<see cref="SlotFallbackPolicy.NearestDirectional"/> only): the nearest existing
    /// directional variant with a POSITIVE dot product against <paramref name="currentDirection"/>;
    /// skipped when the direction is zero-approx or no labels are supplied. Equidistant ties resolve to
    /// insertion order (strict greater-than).
    /// Tier 3 (both policies): the undirected <paramref name="baseName"/> — a perpendicular or opposed
    /// variant is never nearer than the side-view base the facing mirror already handles.
    /// Tier 4 (NearestDirectional only): any existing variant by max dot, so a request with no base
    /// and only off-axis art still shows something rather than nothing.
    /// </summary>
    public static StringName? Resolve(
        Func<StringName, bool> hasAnimation,
        StringName baseName,
        string directionLabel,
        Vector3 currentDirection,
        IReadOnlyDictionary<Vector3, string> directionLabels,
        string separator,
        SlotFallbackPolicy policy)
    {
        var finalName = BuildFinalName(baseName, directionLabel, separator);
        if (hasAnimation(finalName))
        {
            return finalName;
        }

        if (policy == SlotFallbackPolicy.NearestDirectional)
        {
            var nearest = FindClosestAvailableDirectional(hasAnimation, baseName, currentDirection, directionLabels, separator, requirePositiveDot: true);
            if (nearest != null)
            {
                return nearest;
            }
        }

        if (hasAnimation(baseName))
        {
            return baseName;
        }

        if (policy == SlotFallbackPolicy.NearestDirectional)
        {
            return FindClosestAvailableDirectional(hasAnimation, baseName, currentDirection, directionLabels, separator, requirePositiveDot: false);
        }

        return null;
    }

    /// <summary>
    /// Combines an undirected base name with a non-empty directional label.
    /// </summary>
    public static StringName BuildFinalName(StringName baseName, string directionLabel, string separator)
    {
        if (string.IsNullOrEmpty(directionLabel))
        {
            return baseName;
        }
        return new StringName($"{baseName}{separator}{directionLabel}");
    }

    private static StringName? FindClosestAvailableDirectional(
        Func<StringName, bool> hasAnimation,
        StringName baseName,
        Vector3 currentDirection,
        IReadOnlyDictionary<Vector3, string> directionLabels,
        string separator,
        bool requirePositiveDot)
    {
        if (currentDirection.IsZeroApprox() || directionLabels == null)
        {
            return null;
        }

        StringName? best = null;
        var bestDot = float.MinValue;
        foreach (var kvp in directionLabels)
        {
            var candidate = new StringName($"{baseName}{separator}{kvp.Value}");
            if (!hasAnimation(candidate))
            {
                continue;
            }

            var dot = kvp.Key.Dot(currentDirection);
            if (requirePositiveDot && !(dot > 0f))
            {
                continue;
            }

            if (dot > bestDot)
            {
                bestDot = dot;
                best = candidate;
            }
        }

        return best;
    }
}
