namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System.Collections.Generic;
using Core.Visual.Animation.Sprite;
using Godot;

/// <summary>
/// Pure composition of an animation's BASE name from a state's plain stem plus whatever variation
/// the entity's art contributes — "hurt" becomes "hurt_1". Runs upstream of
/// <see cref="DirectionalClipResolver"/>, which then applies its unchanged three-tier ladder to the
/// varied stem, so direction remains entirely the resolver's concern.
/// </summary>
/// <remarks>
/// Static and pure for the same reason the resolver is: it is the one piece of the orchestrator's
/// name assembly that a Logic suite must be able to pin without standing up a Node and an animator.
/// </remarks>
public static class AnimationBaseNameComposer
{
    /// <summary>
    /// Returns <paramref name="baseName"/> verbatim when no source contributes anything — the
    /// equivalence every state that authors no variation depends on. Otherwise applies each
    /// source's contribution in ascending <see cref="AnimVariantSource.Order"/> through
    /// <paramref name="convention"/>, defaulting to a "_"-separated suffix.
    /// </summary>
    public static StringName Compose(
        StringName baseName,
        IReadOnlyList<AnimVariantSource>? sources,
        AnimationNamingConvention? convention)
    {
        if (sources == null || sources.Count == 0)
        {
            return baseName;
        }

        List<string>? variants = null;
        foreach (var source in Ordered(sources))
        {
            var variant = source.SelectAnimVariant(baseName);
            if (string.IsNullOrEmpty(variant))
            {
                continue;
            }

            variants ??= new List<string>(sources.Count);
            variants.Add(variant);
        }

        if (variants == null)
        {
            return baseName;
        }

        // Allocated on first varied composition, never at static init: a Resource constructed
        // before the engine is up would fault a pure-CLR host.
        convention ??= _defaultConvention ??= new SuffixNamingConvention();
        return convention.GetFullAnimationName(baseName, variants);
    }

    // Insertion-stable ascending sort over a list that is authored per entity and never large:
    // List.Sort is unstable, so two sources sharing an Order would swap between frames.
    private static IEnumerable<AnimVariantSource> Ordered(IReadOnlyList<AnimVariantSource> sources)
    {
        var ordered = new List<AnimVariantSource>(sources.Count);
        foreach (var source in sources)
        {
            if (source == null)
            {
                continue;
            }

            var insertAt = ordered.Count;
            while (insertAt > 0 && ordered[insertAt - 1].Order > source.Order)
            {
                insertAt--;
            }
            ordered.Insert(insertAt, source);
        }
        return ordered;
    }

    private static SuffixNamingConvention? _defaultConvention;
}
