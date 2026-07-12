namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System;
using System.Collections.Generic;
using Core.Visual.Animation.Sprite;
using Godot;

/// <summary>
/// Pure resolution of which concrete clip an animator should play for a directional request,
/// degrading through up to three tiers per the caller's <see cref="SlotFallbackPolicy"/>.
/// Extracted from <c>AnimationOrchestrator</c> so both the orchestrator (single leaf target)
/// and <c>CompositeAnimatorComponent</c> (per-slave fan-out) share one resolution rule.
/// </summary>
public static class DirectionalClipResolver
{
    /// <summary>
    /// Resolves the clip name to play, or null when nothing resolves.
    /// Tier 1 (both policies): the exact "{baseName}{separator}{directionLabel}".
    /// Tier 2 (<see cref="SlotFallbackPolicy.NearestDirectional"/> only): the nearest existing
    /// directional variant by max dot product with <paramref name="currentDirection"/>; skipped
    /// when the direction is zero-approx or no labels are supplied. Equidistant ties resolve to
    /// insertion order (strict greater-than), matching the retired FindClosestAvailableDirectional.
    /// Tier 3 (both policies): the undirected <paramref name="baseName"/>.
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
            var nearest = FindClosestAvailableDirectional(hasAnimation, baseName, currentDirection, directionLabels, separator);
            if (nearest != null)
            {
                return nearest;
            }
        }

        if (hasAnimation(baseName))
        {
            return baseName;
        }

        return null;
    }

    private static StringName BuildFinalName(StringName baseName, string directionLabel, string separator)
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
        string separator)
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
            if (dot > bestDot)
            {
                bestDot = dot;
                best = candidate;
            }
        }

        return best;
    }
}
